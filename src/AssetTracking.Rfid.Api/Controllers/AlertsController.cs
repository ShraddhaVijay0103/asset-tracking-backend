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
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AlertsController(AppDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Alert>>> GetAll()
    {
        var list = await _db.Alerts
            .OrderByDescending(a => a.Timestamp)
            .Take(100)
            .ToListAsync();

        return Ok(list);
    }

    [AllowAnonymous]
    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult> Resolve(Guid id)
    {
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.AlertId == id);
        if (alert is null) return NotFound();

        alert.IsResolved = true;
        alert.ResolvedAt = DateTime.UtcNow;
        // For now, we don't have authenticated user id, so leave ResolvedByUser null or set to some default.
        await _db.SaveChangesAsync();

        return Ok(alert);
    }

    [AllowAnonymous]
    [HttpGet("rules")]
    public async Task<ActionResult<AlertRules>> GetRules()
    {
        var rules = await _db.AlertRules.FirstOrDefaultAsync();
        if (rules is null)
        {
            rules = new AlertRules
            {
                AlertRulesId = Guid.NewGuid(),
                MissingItemThreshold = 1,
                OverdueMinutes = 30,
                NotifyEmail = true,
                NotifySms = true,
                NotifyPush = false
            };
            _db.AlertRules.Add(rules);
            await _db.SaveChangesAsync();
        }

        return Ok(rules);
    }

    [AllowAnonymous]
    [HttpPut("rules")]
    public async Task<ActionResult<AlertRules>> UpdateRules([FromBody] AlertRulesUpdateRequest request)
    {
        var rules = await _db.AlertRules.FirstOrDefaultAsync();
        if (rules is null)
        {
            rules = new AlertRules { AlertRulesId = Guid.NewGuid() };
            _db.AlertRules.Add(rules);
        }

        rules.MissingItemThreshold = request.MissingItemThreshold;
        rules.OverdueMinutes = request.OverdueMinutes;
        rules.NotifyEmail = request.NotifyEmail;
        rules.NotifySms = request.NotifySms;
        rules.NotifyPush = request.NotifyPush;

        await _db.SaveChangesAsync();
        return Ok(rules);
    }
}
