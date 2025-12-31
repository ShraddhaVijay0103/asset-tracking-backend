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
