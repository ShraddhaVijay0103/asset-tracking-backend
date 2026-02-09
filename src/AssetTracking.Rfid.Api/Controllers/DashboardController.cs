using AssetTracking.Rfid.Api.Models;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Equipment>>> GetAll()
    {
        var list = await _db.Equipment
            .Include(e => e.EquipmentType)
            .Include(e => e.RfidTag)
            .ToListAsync();

        return Ok(list);
    }

    [AllowAnonymous]
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> GetSummary()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Basic counts
        var totalScansToday = await _db.RfidScans
            .CountAsync(s => s.Timestamp >= today && s.Timestamp < tomorrow);

        var alertsQuery = _db.Alerts.Where(a => !a.IsResolved);
        var activeAlerts = await alertsQuery.CountAsync();
        var criticalAlerts = await alertsQuery.CountAsync(a => a.Severity == "High");

        // Trucks "inside" vs "en route" based on last gate event
        var lastEvents = await _db.GateEvents
            .GroupBy(g => g.TruckId)
            .Select(g => g.OrderByDescending(x => x.EventTime).First())
            .ToListAsync();

        var trucksInside = lastEvents.Count(e => e.EventType == "Entry");
        var trucksEnRoute = lastEvents.Count(e => e.EventType == "Exit");

        // Items leaving / returning today
        var gateEventsToday = await _db.GateEvents
            .Where(g => g.EventTime >= today && g.EventTime < tomorrow)
            .Include(g => g.Items)
            .ToListAsync();

        var itemsLeavingToday = gateEventsToday
            .Where(g => g.EventType == "Exit")
            .Sum(g => g.Items.Count);

        var itemsReturningToday = gateEventsToday
            .Where(g => g.EventType == "Entry")
            .Sum(g => g.Items.Count);

        // Exceptions = missing equipment events today
        var templates = await _db.TruckEquipmentTemplates.ToListAsync();
        var exceptionsToday = 0;
        var expectedTotal = 0;
        var missingTotal = 0;

        foreach (var ge in gateEventsToday.Where(g => g.EventType == "Exit"))
        {
            var expected = templates
                .Where(t => t.TruckId == ge.TruckId)
                .Sum(t => t.RequiredCount);
            var scanned = ge.Items.Count;

            if (expected > 0)
            {
                expectedTotal += expected;
                missingTotal += Math.Max(0, expected - scanned);
                if (expected > scanned)
                {
                    exceptionsToday++;
                }
            }
        }

        double equipmentLossRate = 0;
        if (expectedTotal > 0)
        {
            equipmentLossRate = (double)missingTotal / expectedTotal * 100.0;
        }

        // Reader uptime approx: readers with heartbeat in last 10 min
        var readers = await _db.Readers.ToListAsync();
        var heartbeats = await _db.ReaderHeartbeats
            .GroupBy(h => h.ReaderId)
            .Select(g => new { ReaderId = g.Key, Last = g.Max(x => x.Timestamp) })
            .ToListAsync();

        var hbMap = heartbeats.ToDictionary(x => x.ReaderId, x => x.Last);
        var onlineCount = 0;
        foreach (var r in readers)
        {
            if (hbMap.TryGetValue(r.ReaderId, out var last) &&
                last >= DateTime.UtcNow.AddMinutes(-10))
            {
                onlineCount++;
            }
        }

        double readerUptime = 0;
        if (readers.Count > 0)
        {
            readerUptime = (double)onlineCount / readers.Count * 100.0;
        }

        var summary = new DashboardSummary
        {
            TrucksInside = trucksInside,
            TrucksEnRoute = trucksEnRoute,
            GateEventsLastHour = await _db.GateEvents.CountAsync(g => g.EventTime >= DateTime.UtcNow.AddHours(-1)),
            TotalScansToday = totalScansToday,
            ActiveAlerts = activeAlerts,
            CriticalAlerts = criticalAlerts,
            EquipmentLossRatePercent = Math.Round(equipmentLossRate, 2),
            ReaderUptimePercent = Math.Round(readerUptime, 2),
            ItemsLeavingToday = itemsLeavingToday,
            ItemsReturningToday = itemsReturningToday,
            ExceptionsToday = exceptionsToday
        };

        return Ok(summary);
    }

    [AllowAnonymous]
    [HttpGet("gate-feed")]
    public async Task<ActionResult> GetGateFeed()
    {
        var since = DateTime.UtcNow.AddMinutes(-30);
        var list = await _db.GateEvents
            .Include(g => g.Truck)!.ThenInclude(t => t!.Driver)
            .Include(g => g.Reader)
            .Where(g => g.EventTime >= since)
            .OrderByDescending(g => g.EventTime)
            .Take(50)
            .ToListAsync();

        var projected = list.Select(g => new
        {
            g.GateEventId,
            g.EventTime,
            g.EventType,
            TruckNumber = g.Truck!.TruckNumber,
            DriverName = g.Truck.Driver!.FullName,
            Reader = g.Reader!.Name,
            g.Status
        });

        return Ok(projected);
    }
}
