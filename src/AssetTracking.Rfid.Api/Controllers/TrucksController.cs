using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrucksController : ControllerBase
{
    private readonly AppDbContext _db;

    public TrucksController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Truck>>> GetAll()
    {
        var list = await _db.Trucks
            .Include(t => t.Driver)
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Truck>> GetById(Guid id)
    {
        var truck = await _db.Trucks
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.TruckId == id);

        if (truck is null) return NotFound();
        return Ok(truck);
    }

    [HttpGet("{id:guid}/assignment")]
    public async Task<ActionResult> GetCurrentAssignment(Guid id)
    {
        // Current assignment = equipment with active (not returned) assignments
        var items = await _db.TruckEquipmentAssignments
            .Include(a => a.Equipment)!.ThenInclude(e => e!.EquipmentType)
            .Where(a => a.TruckId == id && a.ReturnedAt == null)
            .Select(a => new
            {
                a.AssignedAt,
                EquipmentName = a.Equipment!.Name,
                EquipmentType = a.Equipment!.EquipmentType!.Name
            })
            .ToListAsync();

        return Ok(items);
    }

    public class AssignTemplateRequest
    {
        public Guid EquipmentTypeId { get; set; }
        public int RequiredCount { get; set; }
    }
    [HttpGet("sites")]
    public async Task<ActionResult<IEnumerable<Site>>> GetSites()
    {
        var list = await _db.Sites
            .OrderBy(s => s.Name)
            .ToListAsync();

        return Ok(list);
    }


    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<Role>>> GetRoles()
    {
        var list = await _db.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();

        return Ok(list);
    }
    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult> AddTemplate(Guid id, [FromBody] AssignTemplateRequest request)
    {
        var template = new TruckEquipmentTemplate
        {
            TemplateId = Guid.NewGuid(),
            TruckId = id,
            EquipmentTypeId = request.EquipmentTypeId,
            RequiredCount = request.RequiredCount
        };
        _db.TruckEquipmentTemplates.Add(template);
        await _db.SaveChangesAsync();
        return Ok(template);
    }
    public class CreateTruckRequest
    {
        public string TruckNumber { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;

        public Guid SiteId { get; set; }
    }

    [HttpPost("create")]
    public async Task<ActionResult<Truck>> CreateTruck([FromBody] CreateTruckRequest request)
    {
        // Check duplicate truck number
        var exists = await _db.Trucks
            .AnyAsync(t => t.TruckNumber == request.TruckNumber);

        if (exists)
            return BadRequest("Truck with this number already exists.");

        // Create driver
        var driver = new Driver
        {
            DriverId = Guid.NewGuid(),
            FullName = request.DriverName
        };

        // Save driver first so DriverId is available
        _db.Drivers.Add(driver);
        await _db.SaveChangesAsync();

        // Create truck
        var truck = new Truck
        {
            TruckId = Guid.NewGuid(),
            TruckNumber = request.TruckNumber,
            Description = "",        // optional
            DriverId = driver.DriverId,
            SiteId = request.SiteId
        };

        _db.Trucks.Add(truck);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Truck created successfully",
            truck
        });
    }

}
