using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/audit-logs")]
public class AdminAuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminAuditLogsController(AppDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLog>>> GetAuditLogs()
    {
        var list = await _db.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(200)
            .ToListAsync();

        return Ok(list);
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
}
