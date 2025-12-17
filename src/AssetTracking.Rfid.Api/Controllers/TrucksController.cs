using AssetTracking.Rfid.Api.Models;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    public async Task<ActionResult<IEnumerable<TruckListResponse>>> GetAll()
    {
        var list = await _db.Trucks
            .Include(t => t.Site)
            .Include(t => t.Driver)
            .Select(t => new TruckListResponse
            {
                TruckId = t.TruckId,
                TruckNumber = t.TruckNumber,
                SiteName = t.Site.Name,
                DriverName = t.Driver.FullName
            })
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
    public class CreateTruckRequest
    {
        public string TruckNumber { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public Guid SiteId { get; set; }
    }
    [HttpPut("{id:guid}/assign")]
    public async Task<ActionResult> AddTemplate(Guid id, [FromBody] CreateTruckRequest request)
    {
        // 1️⃣ Get existing truck
        var truck = await _db.Trucks
                             .FirstOrDefaultAsync(t => t.TruckId == id);

        if (truck == null)
        {
            return NotFound($"Truck with ID {id} not found");
        }
        var driver = await _db.Drivers
                             .FirstOrDefaultAsync(t => t.FullName == request.DriverName);

        // 2️⃣ Update fields from request
        truck.TruckNumber = request.TruckNumber;
        truck.DriverId = driver.DriverId;
        truck.SiteId = request.SiteId;

        // 3️⃣ Save changes (NO Add)
        _db.Trucks.Update(truck);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Truck updated successfully",
            truck
        });
    }

    [HttpPost("create")]
    public async Task<ActionResult<Truck>> CreateTruck([FromBody] CreateTruckRequest request)
    {
        // 1️⃣ Check duplicate truck number
        var exists = await _db.Trucks
            .AnyAsync(t => t.TruckNumber == request.TruckNumber);

        if (exists)
            return BadRequest("Truck with this number already exists.");

        // 2️⃣ Get or create driver
        var driver = await _db.Drivers
            .FirstOrDefaultAsync(d => d.FullName == request.DriverName);

        if (driver == null)
        {
            driver = new Driver
            {
                DriverId = Guid.NewGuid(),
                FullName = request.DriverName
            };

            _db.Drivers.Add(driver);
            await _db.SaveChangesAsync(); // Save to get DriverId
        }

        // 3️⃣ Create truck
        var truck = new Truck
        {
            TruckId = Guid.NewGuid(),
            TruckNumber = request.TruckNumber,
            Description = string.Empty,
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

    [HttpGet("trucks")]
    public async Task<ActionResult<IEnumerable<Truck>>> GetTruck()
    {
        var list = await _db.Trucks
            .OrderBy(r => r.TruckNumber)
            .ToListAsync();

        return Ok(list);
    }

    public class DriverRequest
    {
        public Guid userId { get; set; }
        public string driverName { get; set; } = string.Empty;
     
    }

    [HttpGet("drivers")]
    public async Task<ActionResult<IEnumerable<DriverRequest>>> GetDrivers()
    {
        var drivers = await _db.Users
            .Where(u => u.Role.Name == "Driver")
            .Select(u => new DriverRequest
            {
                userId = u.UserId,
                driverName = u.FullName
            })
            .ToListAsync();

        return Ok(drivers);
    }

    [HttpGet("EquipmentAssignments")]
    public async Task<ActionResult<IEnumerable<TruckEquipmentAssignmentDto>>> GetDataAll()
    {
        var list = await _db.TruckEquipmentAssignments
            .Include(a => a.Truck)
                .ThenInclude(t => t.Driver)
            .Include(a => a.Equipment)
            .OrderByDescending(a => a.AssignmentId)
            .Take(100)
            .ToListAsync();

        return Ok(list);
    }

}
