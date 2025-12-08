using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
public class AdminAuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminAuditLogsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLog>>> GetAuditLogs()
    {
        var list = await _db.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(200)
            .ToListAsync();

        return Ok(list);
    }
}
