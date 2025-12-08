using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssetTracking.Rfid.Infrastructure.Persistence;
using AssetTracking.Rfid.Api.Models;
using AssetTracking.Rfid.Domain.Entities;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Route("api/rfid")]
public class RfidController : ControllerBase
{
    private readonly AppDbContext _db;

    public RfidController(AppDbContext db)
    {
        _db = db;
    }

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
}
