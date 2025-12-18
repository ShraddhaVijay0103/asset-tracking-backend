using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Route("api/admin/rfid")]
public class AdminRfidController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminRfidController(AppDbContext db)
    {
        _db = db;
    }

    public class AssignTagRequest
    {
        public string Epc { get; set; } = string.Empty;
        public Guid EquipmentId { get; set; }
    }

    [HttpPost("assign")]
    public async Task<ActionResult> AssignTag([FromBody] AssignTagRequest request)
    {
        var tag = await _db.RfidTags.FirstOrDefaultAsync(t => t.Epc == request.Epc);
        if (tag == null)
        {
            tag = new RfidTag
            {
                RfidTagId = Guid.NewGuid(),
                Epc = request.Epc
            };
            _db.RfidTags.Add(tag);
        }

        var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.EquipmentId == request.EquipmentId);
        if (equipment == null) return NotFound($"Equipment {request.EquipmentId} not found.");

        equipment.RfidTagId = tag.RfidTagId;
        await _db.SaveChangesAsync();

        return Ok(new { equipment.EquipmentId, equipment.Name, TagEpc = tag.Epc });
    }
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EquipmentType>>> GetAll()
    {
        var list = await _db.RfidTags.ToListAsync();
        return Ok(list);
    }
}
