using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EquipmentController : ControllerBase
{
    private readonly AppDbContext _db;

    public EquipmentController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Equipment>>> GetAll()
    {
        var list = await _db.Equipment
            .Include(e => e.EquipmentType)
            .Include(e => e.RfidTag)
            .ToListAsync();

        return Ok(list);
    }

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
