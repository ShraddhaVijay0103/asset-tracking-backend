using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using AssetTracking.Rfid.Api.Models;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GateEventsController : ControllerBase
{
    private readonly AppDbContext _db;

    public GateEventsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<GateEvent>>> GetAll()
    {
        var list = await _db.GateEvents
            .Include(g => g.Truck)!.ThenInclude(t => t!.Driver)
            .Include(g => g.Reader)
            .Include(g => g.Items)!.ThenInclude(i => i.Equipment)
            .OrderByDescending(g => g.EventTime)
            .Take(100)
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("live")]
    public async Task<ActionResult<IEnumerable<GateEvent>>> GetLive()
    {
        //var since = DateTime.UtcNow.AddMinutes(-5);
        var list = await _db.GateEvents
            .Include(g => g.Truck)!.ThenInclude(t => t!.Driver)
            .Include(g => g.Reader)
            //.Where(g => g.EventTime >= since)
            .OrderByDescending(g => g.EventTime)
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GateEvent>> GetById(Guid id)
    {
        var gateEvent = await _db.GateEvents
            .Include(g => g.Truck)!.ThenInclude(t => t!.Driver)
            .Include(g => g.Reader)
            .Include(g => g.Items)!.ThenInclude(i => i.Equipment)
            .FirstOrDefaultAsync(g => g.GateEventId == id);

        if (gateEvent is null) return NotFound();

        return Ok(gateEvent);
    }

    [HttpPost("{id:guid}/review")]
    public async Task<ActionResult> Review(Guid id, [FromBody] GateEventReviewRequest request)
    {
        var gateEvent = await _db.GateEvents.FirstOrDefaultAsync(g => g.GateEventId == id);
        if (gateEvent is null) return NotFound();

        gateEvent.Status = request.Status;
        gateEvent.Notes = request.Notes;
        await _db.SaveChangesAsync();

        return Ok(gateEvent);
    }
}
