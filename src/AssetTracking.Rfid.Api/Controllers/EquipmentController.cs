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
                        Opened_At = mec.OpenedAt
                    };

        var result = await query.AsNoTracking().ToListAsync();

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

        // Fetch the case with its items
        var caseRecord = await _db.MissingEquipmentCases
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.MissingEquipmentCaseId == request.MissingEquipmentCaseId
                                   && c.SiteId == siteId);

        if (caseRecord == null)
            return NotFound("Missing equipment case not found.");

        // --- Update Notes ---
        if (!string.IsNullOrEmpty(request.Notes))
            caseRecord.Notes = request.Notes;

        // --- Update Status from request ---
        caseRecord.StatusId = request.StatusId;

        // --- Update LastSeenAt automatically ---
        caseRecord.LastSeenAt = DateTimeOffset.UtcNow;

        // --- Handle automatic status transitions ---
        switch (request.StatusId)
        {
            case 1: // Open
                    // Nothing to do, remains Open
                break;
            case 2: // Investigation
                    // Move to Investigation
                caseRecord.StatusId = 2;
                break;
            case 3: // Recovered
                    // Mark all recovered
                caseRecord.ClosedAt = null; // Not closed yet
                caseRecord.StatusId = 3;
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
                    // Close case fully
                caseRecord.ClosedAt = DateTimeOffset.UtcNow;
                caseRecord.StatusId = 4;
                break;
            default:
                return BadRequest("Invalid StatusId. Must be 1 (Open), 2 (Investigation), 3 (Recovered), 4 (Closed).");
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

        if (todayMissing == 0 && thisWeekMissing == 0 && thisMonthMissing == 0
            && longOverdue == 0 && recentlyClearedToday == 0)
            return NotFound();

        return Ok(response);
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
