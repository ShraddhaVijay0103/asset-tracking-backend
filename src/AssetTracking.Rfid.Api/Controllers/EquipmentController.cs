using AssetTracking.Rfid.Api.Models;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class EquipmentController : ControllerBase
{
    private readonly AppDbContext _db;

    public EquipmentController(AppDbContext db)
    {
        _db = db;
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
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Equipment>> GetById(Guid id)
    {
        var equipment = await _db.Equipment
            .Include(e => e.EquipmentType)
            .Include(e => e.RfidTag)
            .FirstOrDefaultAsync(e => e.EquipmentId == id);

        if (equipment is null) return NotFound();

        return Ok(equipment);
    }


    [AllowAnonymous]
    [HttpGet("{siteId:guid}/missing-equipment")]
    public async Task<IActionResult> GetMissingEquipmentTable(Guid siteId)
    {
        var query = from mec in _db.MissingEquipmentCases
                    join meci in _db.MissingEquipmentCaseItems
                        on mec.MissingEquipmentCaseId equals meci.MissingEquipmentCaseId
                    join e in _db.Equipment
                        on meci.EquipmentId equals e.EquipmentId
                    join mes in _db.MissingEquipmentStatuses
                        on mec.StatusId equals mes.StatusId
                    join mesv in _db.MissingEquipmentSeverities
                        on mec.SeverityId equals mesv.SeverityId
                    join t in _db.Trucks
                        on mec.TruckId equals t.TruckId into tLeft
                    from t in tLeft.DefaultIfEmpty()
                    join d in _db.Drivers
                        on mec.DriverId equals d.DriverId into dLeft
                    from d in dLeft.DefaultIfEmpty()
                    join s in _db.Sites
                        on t.SiteId equals s.SiteId into sLeft
                    from s in sLeft.DefaultIfEmpty()
                    where mec.ClosedAt == null
                          && (t != null && t.SiteId == siteId)
                    orderby mec.OpenedAt descending
                    select new
                    {
                        mec.MissingEquipmentCaseId,
                        Equipment_Id = e.EquipmentId,
                        Equipment_Name = e.Name,
                        Truck = t != null ? t.TruckNumber : null,
                        Driver = d != null ? d.FullName : null,
                        Site_Name = s != null ? s.Name : null,
                        Status_Id = mes.StatusId,
                        Status = mes.Code,
                        Severity = mesv.Code,
                        Opened_At = mec.OpenedAt,
                        lastSeen = mec.LastSeenAt
                    };

        var result = await query.AsNoTracking().ToListAsync();
        if (!result.Any())
        {
            result.Add(new
            {
                MissingEquipmentCaseId = Guid.Empty,
                Equipment_Id = Guid.Empty,
                Equipment_Name = "N/A",
                Truck = "N/A",
                Driver = "N/A",
                Site_Name = "N/A",
                Status_Id = 0,
                Status = "N/A",
                Severity = "N/A",
                Opened_At = DateTimeOffset.MinValue,
                lastSeen = (DateTimeOffset?)null
            });
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPut("{siteId:guid}/missing-equipment-investigation")]
    public async Task<IActionResult> UpdateMissingEquipmentInvestigation(
      Guid siteId,
      [FromBody] CreateInvestigationRequest request)
    {
        if (request == null || request.MissingEquipmentCaseId == Guid.Empty)
            return BadRequest("Invalid request data.");

        var caseRecord = await _db.MissingEquipmentCases
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c =>
                c.MissingEquipmentCaseId == request.MissingEquipmentCaseId &&
                c.SiteId == siteId);

        if (caseRecord == null)
            return NotFound("Missing equipment case not found.");

        // Always update last seen
        caseRecord.LastSeenAt = DateTimeOffset.UtcNow;

        switch (request.StatusId)
        {
            case 1: // Open
                caseRecord.StatusId = 1;
                if (!string.IsNullOrEmpty(request.Notes))
                    caseRecord.OpenNotes = request.Notes;
                break;

            case 2: // Investigation
                caseRecord.StatusId = 2;
                if (!string.IsNullOrEmpty(request.Notes))
                    caseRecord.InvestigationNotes = request.Notes;
                break;

            case 3: // Recovered
                caseRecord.StatusId = 3;
                if (!string.IsNullOrEmpty(request.Notes))
                    caseRecord.RecoveredNotes = request.Notes;

                // Mark all items as recovered
                foreach (var item in caseRecord.Items)
                {
                    if (!item.IsRecovered)
                    {
                        item.IsRecovered = true;
                        item.RecoveredAt = DateTimeOffset.UtcNow;
                    }
                }
                break;

            case 4: // Closed
                caseRecord.StatusId = 4;
                caseRecord.ClosedAt = DateTimeOffset.UtcNow;
                if (!string.IsNullOrEmpty(request.Notes))
                    caseRecord.ClosedNotes = request.Notes;
                break;

            default:
                return BadRequest("Invalid StatusId. Allowed values: 1, 2, 3, 4.");
        }

        await _db.SaveChangesAsync();
        return Ok(caseRecord);
    }

    [AllowAnonymous]
    [HttpGet("{siteId:guid}/missing-equipment-summary")]
    public async Task<ActionResult<MissingEquipmentCountsDto>> GetMissingEquipmentSummary(Guid siteId)
    {
        var nowUtc = DateTime.UtcNow; // Use UTC

        // Today (UTC)
        var todayStartUtc = nowUtc.Date;
        var todayEndUtc = todayStartUtc.AddDays(1);

        // This week (Monday to Sunday, UTC)
        var diff = (7 + (nowUtc.DayOfWeek - DayOfWeek.Monday)) % 7;
        var weekStartUtc = todayStartUtc.AddDays(-diff);
        var weekEndUtc = weekStartUtc.AddDays(7);

        // This month (UTC)
        var monthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEndUtc = monthStartUtc.AddMonths(1);

        // Flatten all items with parent OpenedAt
        var itemsQuery = _db.MissingEquipmentCases
            .Where(c => c.SiteId == siteId)
            .SelectMany(c => c.Items, (c, i) => new
            {
                Item = i,
                OpenedAt = c.OpenedAt.UtcDateTime, // Ensure UTC
                ClosedAt = c.ClosedAt.HasValue ? c.ClosedAt.Value.UtcDateTime : (DateTime?)null,
                RecoveredAt = i.RecoveredAt.HasValue ? i.RecoveredAt.Value.UtcDateTime : (DateTime?)null
            });

        // Today missing
        var todayMissing = await itemsQuery
            .Where(x => x.ClosedAt == null
                        && !x.Item.IsRecovered
                        && x.OpenedAt >= todayStartUtc
                        && x.OpenedAt < todayEndUtc)
            .CountAsync();

        // This week missing
        var thisWeekMissing = await itemsQuery
            .Where(x => x.ClosedAt == null
                        && !x.Item.IsRecovered
                        && x.OpenedAt >= weekStartUtc
                        && x.OpenedAt < weekEndUtc)
            .CountAsync();

        // This month missing
        var thisMonthMissing = await itemsQuery
            .Where(x => x.ClosedAt == null
                        && !x.Item.IsRecovered
                        && x.OpenedAt >= monthStartUtc
                        && x.OpenedAt < monthEndUtc)
            .CountAsync();

        // Long overdue (>24h)
        var longOverdue = await itemsQuery
            .Where(x => x.ClosedAt == null
                        && !x.Item.IsRecovered
                        && (nowUtc - x.OpenedAt).TotalHours > 24)
            .CountAsync();

        // Recently cleared today
        var recentlyClearedToday = await itemsQuery
            .Where(x => x.Item.IsRecovered
                        && x.RecoveredAt.HasValue
                        && x.RecoveredAt >= todayStartUtc
                        && x.RecoveredAt < todayEndUtc)
            .CountAsync();

        var response = new MissingEquipmentCountsDto
        {
            TodayMissing = todayMissing,
            ThisWeekMissing = thisWeekMissing,
            ThisMonthMissing = thisMonthMissing,
            LongOverdue = longOverdue,
            RecentlyClearedToday = recentlyClearedToday
        };

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("{siteId:guid}/quick-alerts")]
    public async Task<IActionResult> GetQuickAlerts(Guid siteId)
    {
        var nowUtc = DateTime.UtcNow;

        var itemsQuery = _db.MissingEquipmentCases
            .Where(c => c.SiteId == siteId && c.ClosedAt == null)
            .SelectMany(c => c.Items, (c, i) => new
            {
                OpenedAt = c.OpenedAt.UtcDateTime,
                RecoveredAt = i.RecoveredAt,
                SeverityId = c.SeverityId
            })
            .Where(x => x.RecoveredAt == null && x.SeverityId >= 3);

        var criticalSeverityCount = await itemsQuery
            .Where(x => x.SeverityId > 3)
            .CountAsync();

        var agingCasesCount = await itemsQuery
            .Where(x => (nowUtc - x.OpenedAt).TotalDays > 7)
            .CountAsync();

        return Ok(new
        {
            SeverityCount = criticalSeverityCount,
            AgingCasesCount = agingCasesCount
        });
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult> GetHistory(Guid id)
    {
        var history = await _db.GateEventItems
            .Include(i => i.GateEvent)!.ThenInclude(g => g!.Truck)
            .Where(i => i.EquipmentId == id)
            .OrderByDescending(i => i.GateEvent!.EventTime)
            .Select(i => new
            {
                i.GateEvent!.EventTime,
                i.GateEvent.EventType,
                TruckNumber = i.GateEvent.Truck!.TruckNumber,
                i.GateEvent.Status,
                Reader = i.GateEvent.Reader!.Name
            })
            .ToListAsync();

        return Ok(history);
    }
}
