using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/equipment-types")]
public class AdminEquipmentTypesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminEquipmentTypesController(AppDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EquipmentType>>> GetAll()
    {
        var list = await _db.EquipmentTypes.ToListAsync();
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
    [HttpPost]
    public async Task<ActionResult<EquipmentType>> Create([FromBody] EquipmentType type)
    {
        if (type.EquipmentTypeId == Guid.Empty)
            type.EquipmentTypeId = Guid.NewGuid();

        _db.EquipmentTypes.Add(type);
        await _db.SaveChangesAsync();
        return Ok(type);
    }
}
