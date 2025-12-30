using AssetTracking.Rfid.Api.Models;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet("missing-equipment")]
    public async Task<ActionResult<IEnumerable<MissingEquipmentReportRow>>> GetMissingEquipment([FromQuery] DateOnly date)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = date.ToDateTime(TimeOnly.MaxValue);

        var events = await _db.GateEvents
            .Include(g => g.Truck)!.ThenInclude(t => t!.Driver)
            .Include(g => g.Reader)
            .Include(g => g.Items)
            .Where(g => g.EventTime >= start && g.EventTime <= end && g.EventType == "Exit")
            .ToListAsync();

        var rows = new List<MissingEquipmentReportRow>();

        foreach (var ge in events)
        {
            var expected = await _db.TruckEquipmentTemplates
                .Where(t => t.TruckId == ge.TruckId)
                .SumAsync(t => (int?)t.RequiredCount) ?? 0;

            var scanned = ge.Items.Count;

            if (expected > scanned)
            {
                rows.Add(new MissingEquipmentReportRow
                {
                    EventTime = ge.EventTime,
                    TruckNumber = ge.Truck?.TruckNumber ?? string.Empty,
                    DriverName = ge.Truck?.Driver?.FullName ?? string.Empty,
                    ReaderName = ge.Reader?.Name ?? string.Empty,
                    ExpectedCount = expected,
                    ScannedCount = scanned,
                    MissingCount = expected - scanned
                });
            }
        }

        return Ok(rows.OrderByDescending(r => r.EventTime));
    }

    [AllowAnonymous]
    [HttpGet("truck-history/{truckId:guid}")]
    public async Task<ActionResult<IEnumerable<TruckHistoryRow>>> GetTruckHistory(Guid truckId)
    {
        var list = await _db.GateEvents
            .Include(g => g.Reader)
            .Where(g => g.TruckId == truckId)
            .OrderByDescending(g => g.EventTime)
            .Select(g => new TruckHistoryRow
            {
                EventTime = g.EventTime,
                EventType = g.EventType,
                ReaderName = g.Reader!.Name,
                Status = g.Status
            })
            .ToListAsync();

        return Ok(list);
    }

    [AllowAnonymous]
    [HttpGet("reader-health")]
    public async Task<ActionResult<IEnumerable<ReaderHealthRow>>> GetReaderHealth()
    {
        var readers = await _db.Readers.ToListAsync();
        var latestHeartbeats = await _db.ReaderHeartbeats
            .GroupBy(h => h.ReaderId)
            .Select(g => new { ReaderId = g.Key, Last = g.Max(h => h.Timestamp) })
            .ToListAsync();

        var map = latestHeartbeats.ToDictionary(x => x.ReaderId, x => x.Last);

        var rows = readers.Select(r =>
        {
            //map.TryGetValue(r.ReaderId, out var last);
            DateTime? last = map.TryGetValue(r.ReaderId, out var value) ? value : (DateTime?)null;

            // var isOnline = last.HasValue && last.Value >= DateTime.UtcNow.AddMinutes(-10);
            var isOnline = last.HasValue && last >= DateTime.UtcNow.AddMinutes(-10);

            return new ReaderHealthRow
            {
                ReaderName = r.Name,
                Location = r.Location,
                LastHeartbeat = last,
                IsOnline = isOnline
            };
        }).ToList();

        return Ok(rows);
    }

    [AllowAnonymous]
    [HttpGet("driver-history/{driverId:guid}")]
    public async Task<ActionResult<IEnumerable<TruckHistoryRow>>> GetDriverHistory(Guid driverId)
    {
        var list = await _db.GateEvents
            .Include(g => g.Reader)
            .Include(g => g.Truck)
            .Where(g => g.DriverId == driverId)
            .OrderByDescending(g => g.EventTime)
            .Select(g => new TruckHistoryRow
            {
                EventTime = g.EventTime,
                EventType = g.EventType,
                ReaderName = g.Reader!.Name,
                Status = g.Status
            })
            .ToListAsync();

        return Ok(list);
    }

    [AllowAnonymous]
    [HttpGet("equipment-utilization")]
    public async Task<ActionResult> GetEquipmentUtilization([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var start = from ?? DateTime.UtcNow.AddDays(-30);
        var end = to ?? DateTime.UtcNow;

        var usage = await _db.GateEventItems
            .Include(i => i.Equipment)!.ThenInclude(e => e!.EquipmentType)
            .Include(i => i.GateEvent)
            .Where(i => i.GateEvent!.EventTime >= start && i.GateEvent.EventTime <= end)
            .GroupBy(i => new { i.EquipmentId, i.Equipment!.Name, Type = i.Equipment!.EquipmentType!.Name })
            .Select(g => new
            {
                g.Key.EquipmentId,
                g.Key.Name,
                g.Key.Type,
                Uses = g.Count()
            })
            .OrderByDescending(x => x.Uses)
            .ToListAsync();

        return Ok(usage);
    }

    [AllowAnonymous]
    [HttpGet("rfid-analytics")]
    public async Task<ActionResult> GetRfidAnalytics([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var start = from ?? DateTime.UtcNow.AddHours(-24);
        var end = to ?? DateTime.UtcNow;

        var byHour = await _db.RfidScans
            .Where(s => s.Timestamp >= start && s.Timestamp <= end)
            .GroupBy(s => new { Hour = new DateTime(s.Timestamp.Year, s.Timestamp.Month, s.Timestamp.Day, s.Timestamp.Hour, 0, 0, DateTimeKind.Utc) })
            .Select(g => new
            {
                g.Key.Hour,
                Count = g.Count()
            })
            .OrderBy(x => x.Hour)
            .ToListAsync();

        return Ok(byHour);
    }

    [AllowAnonymous]
    [HttpGet("exceptions-resolution")]
    public async Task<ActionResult> GetExceptionsResolution([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var start = from ?? DateTime.UtcNow.AddDays(-30);
        var end = to ?? DateTime.UtcNow;

        var alerts = await _db.Alerts
            .Where(a => a.Timestamp >= start && a.Timestamp <= end && a.Source == "GateEvent")
            .Select(a => new
            {
                a.AlertId,
                a.Timestamp,
                a.Message,
                a.Severity,
                a.IsResolved,
                a.ResolvedAt
            })
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        return Ok(alerts);
    }

}
