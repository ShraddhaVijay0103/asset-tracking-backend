using AssetTracking.Rfid.Api.Models;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TrucksController : ControllerBase
{
    private readonly AppDbContext _db;

    public TrucksController(AppDbContext db)
    {
        _db = db;
    }


    [AllowAnonymous]
    [HttpGet("site/{siteId:guid}")]
    public async Task<ActionResult<IEnumerable<TruckListResponse>>> GetBySite(Guid siteId)
    {
        var list = await _db.Trucks
            .Where(t => t.SiteId == siteId)
            .Include(t => t.Site)
            .Include(t => t.Driver)
            .Include(t => t.RfidTag)
            .Select(t => new TruckListResponse
            {
                TruckId = t.TruckId,
                TruckNumber = t.TruckNumber,
                SiteName = t.Site.Name,
                DriverName = t.Driver.FullName,
                RfIdTag = t.RfidTag.TagName
            })
            .ToListAsync();

        return Ok(list);
    }


    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Truck>> GetById(Guid id)
    {
        var truck = await _db.Trucks
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.TruckId == id);

        if (truck is null) return NotFound();
        return Ok(truck);
    }

    [AllowAnonymous]
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

    [AllowAnonymous]
    [HttpGet("sites")]
    public async Task<ActionResult<IEnumerable<Site>>> GetSites()
    {
        var list = await _db.Sites
            .OrderBy(s => s.Name)
            .ToListAsync();

        return Ok(list);
    }

    [AllowAnonymous]
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
        public Guid RfidTagId { get; set; }
    }

    [AllowAnonymous]
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

    [AllowAnonymous]
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
            SiteId = request.SiteId,
            RfidTagId = request.RfidTagId
        };

        _db.Trucks.Add(truck);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Truck created successfully",
            truck
        });
    }

    [AllowAnonymous]
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

    [AllowAnonymous]
    [HttpGet("drivers")]
    public async Task<ActionResult<IEnumerable<DriverRequest>>> GetDrivers()
    {
        var drivers = await _db.Drivers
            .Select(d => new DriverRequest
            {
                userId = d.DriverId,
                driverName = d.FullName
            })
            .ToListAsync();

        return Ok(drivers);
    }

    [AllowAnonymous]
    [HttpGet("TruckData")]
    public async Task<ActionResult<IEnumerable<TruckEquipmentAssignmentDto>>> GetTruckDataAll()
    {
        var list = await _db.Trucks
            .Include(t => t.Driver)
             .Include(d => d.RfidTag)
            .OrderByDescending(t => t.TruckNumber)
            .Take(100)
            .ToListAsync();

        return Ok(list);
    }


    [AllowAnonymous]
    [HttpGet("site/{siteId:guid}/complete-status")]
    public async Task<IActionResult> GetCompleteStatusBySite(Guid siteId)
    {
        // Get all trucks for the site
        var trucks = await _db.Trucks
            .Where(t => t.SiteId == siteId)
            .Include(t => t.Driver)
            .ToListAsync();

        if (!trucks.Any())
            return NotFound(new { Message = "No trucks found for this site" });

        var results = new List<object>();

        foreach (var truck in trucks)
        {
            var truckId = truck.TruckId;

            var truckPresent = await _db.GateEvents
                .AnyAsync(g => g.TruckId == truck.TruckId && g.SiteId == siteId);

            if (!truckPresent)
                continue;

            // ================= 1. GET TRUCK TEMPLATES =================
            var templates = await _db.TruckEquipmentTemplates
                .Where(t => t.TruckId == truckId && t.SiteId == siteId)
                .Include(t => t.EquipmentType)
                .ToListAsync();

            // ================= 2. GET LATEST GATE EVENTS =================
            var lastEntryEvent = await _db.GateEvents
                .Where(e => e.TruckId == truckId && e.EventType == "Entry" && e.SiteId == siteId)
                .OrderByDescending(e => e.EventTime)
                .FirstOrDefaultAsync();

            var lastExitEvent = await _db.GateEvents
                .Where(e => e.TruckId == truckId && e.EventType == "Exit" && e.SiteId == siteId)
                .OrderByDescending(e => e.EventTime)
                .FirstOrDefaultAsync();

            // ================= 3. GET ENTRY EQUIPMENT =================
            var entryEquipment = new List<Equipment>();
            var entryEquipmentIds = new List<Guid>();

            if (lastEntryEvent != null)
            {
                entryEquipmentIds = await _db.GateEventItems
                    .Where(gei => gei.GateEventId == lastEntryEvent.GateEventId && gei.SiteId == siteId)
                    .Select(gei => gei.EquipmentId)
                    .ToListAsync();

                entryEquipment = await _db.Equipment
                    .Where(eq => entryEquipmentIds.Contains(eq.EquipmentId))
                    .Include(e => e.EquipmentType)
                    .ToListAsync();
            }

            // ================= 4. GET EXIT EQUIPMENT =================
            var exitEquipment = new List<Equipment>();
            var exitEquipmentIds = new List<Guid>();

            if (lastExitEvent != null)
            {
                exitEquipmentIds = await _db.GateEventItems
                    .Where(gei => gei.GateEventId == lastExitEvent.GateEventId && gei.SiteId == siteId)
                    .Select(gei => gei.EquipmentId)
                    .ToListAsync();

                exitEquipment = await _db.Equipment
                    .Where(eq => exitEquipmentIds.Contains(eq.EquipmentId))
                    .Include(e => e.EquipmentType)
                    .ToListAsync();
            }

            // ================= 5. BUILD CHECK-OUT TABLE =================

            var checkoutTable = entryEquipment.Select(e => new
            {
                Equipment = e.Name,
                Required = 1,
                Detected = "✓",
                EquipmentId = e.EquipmentId
            }).ToList();


            var checkOutSummary = new
            {
                totalRequired = checkoutTable.Count,
                totalAssigned = entryEquipment.Count
            };
            // ================= 6. BUILD CHECK-IN TABLE =================

            var checkinTable = exitEquipment.Select(e => new
            {
                Equipment = e.Name,
                GateStatus = "MISSING",
                EquipmentId = e.EquipmentId
            }).ToList();

            var checkInSummary = new
            {
                totalRequired = checkoutTable.Count,
                totalAssigned = exitEquipment.Count
            };

            // Add result for this truck
            results.Add(new
            {
                Truck = new
                {
                    truck.TruckId,
                    truck.TruckNumber,
                    Driver = truck.Driver?.FullName
                },
                CheckOut = new
                {
                    Table = checkoutTable,
                    Summary = new
                    {
                        TotalRequired = templates.Sum(t => t.RequiredCount),
                        TotalAssigned = exitEquipment.Count
                    }
                },
                CheckIn = new
                {
                    LastCheckinTime = lastEntryEvent?.EventTime,
                    Table = checkinTable,
                    Summary = new
                    {
                        TotalExpected = entryEquipment.Count,
                        TotalDetected = exitEquipment.Count,
                        MissingCount = checkinTable.Count
                    }
                }
            });
        }

        return Ok(new
        {
            SiteId = siteId,
            TotalTrucks = results.Count,
            Trucks = results
        });
    }

    // Helper classes for structured response
    public class CheckoutRow
    {
        public string Equipment { get; set; }
        public int Required { get; set; }
        public string Detected { get; set; }
        public Guid? EquipmentId { get; set; }
    }

    public class CheckinRow
    {
        public string Equipment { get; set; }
        public string GateStatus { get; set; }
        public Guid? EquipmentId { get; set; }
    }




}
