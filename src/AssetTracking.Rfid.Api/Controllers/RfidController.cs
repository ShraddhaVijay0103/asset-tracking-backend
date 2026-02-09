using AssetTracking.Rfid.Api.Models;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rfid")]
public class RfidController : ControllerBase
{
    private readonly AppDbContext _db;

    public RfidController(AppDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpPost("ingest")]
    public async Task<ActionResult> Ingest([FromBody] RfidEventBatch batch)
    {
        foreach (var e in batch.Events)
        {
            var scan = new RfidScan
            {
                ScanId = e.ScanId == Guid.Empty ? Guid.NewGuid() : e.ScanId,
                Epc = e.Epc,
                Rssi = e.Rssi,
                ReaderId = batch.ReaderId,
                SiteId = batch.SiteId,
                Timestamp = e.Timestamp
            };
            _db.RfidScans.Add(scan);
        }

        await _db.SaveChangesAsync();
        return Ok(new { Count = batch.Events.Count });
    }

    [AllowAnonymous]
    [HttpGet("rfidtaglist/{siteId:guid}")]
    public async Task<ActionResult<IEnumerable<RfidTagListResponse>>> GetRfidTagList(Guid siteId)
    {
        var list = await _db.RfidTags
            .Where(r => r.SiteId == siteId && r.IsActive)
            .Where(r => !_db.Trucks.Any(t => t.RfidTagId == r.RfidTagId))
            .Where(r => !_db.Equipment.Any(t => t.RfidTagId == r.RfidTagId))
            .Select(r => new RfidTagListResponse
            {
                RfidTagId = r.RfidTagId,
                Epc = r.TagName
            })
            .ToListAsync();

        return Ok(list);
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
}
