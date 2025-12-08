using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssetTracking.Rfid.Infrastructure.Persistence;
using AssetTracking.Rfid.Api.Models;
using AssetTracking.Rfid.Domain.Entities;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Route("api/workflow")]
public class WorkflowController : ControllerBase
{
    private readonly AppDbContext _db;

    public WorkflowController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<WorkflowResult>> Checkout([FromBody] CheckoutRequest request)
    {
        return await HandleGateWorkflow(request.TruckId, request.DriverId, request.ReaderId, request.SiteId, request.Items, "Exit");
    }

    [HttpPost("checkin")]
    public async Task<ActionResult<WorkflowResult>> Checkin([FromBody] CheckinRequest request)
    {
        return await HandleGateWorkflow(request.TruckId, request.DriverId, request.ReaderId, request.SiteId, request.Items, "Entry");
    }

    private async Task<WorkflowResult> HandleGateWorkflow(
        Guid truckId,
        Guid? driverId,
        Guid? readerId,
        string siteId,
        List<WorkflowScanItem> scanItems,
        string eventType)
    {
        var now = DateTime.UtcNow;

        var gateEvent = new GateEvent
        {
            GateEventId = Guid.NewGuid(),
            TruckId = truckId,
            DriverId = driverId,
            ReaderId = readerId ?? Guid.Empty,
            EventTime = now,
            EventType = eventType,
            Status = "Pending",
            Notes = $"{eventType} processed via workflow API at {now:u}"
        };

        _db.GateEvents.Add(gateEvent);

        // Recognize scanned EPCs -> Equipment
        var epcs = scanItems.Select(i => i.Epc).ToList();
        var equipmentByEpc = await _db.Equipment
            .Include(e => e.EquipmentType)
            .Include(e => e.RfidTag)
            .Where(e => e.RfidTag != null && epcs.Contains(e.RfidTag.Epc))
            .ToListAsync();

        var result = new WorkflowResult
        {
            GateEventId = gateEvent.GateEventId,
            EventType = eventType
        };

        // Add GateEventItems for recognized equipment
        foreach (var scan in scanItems)
        {
            var eq = equipmentByEpc.FirstOrDefault(e => e.RfidTag!.Epc == scan.Epc);
            if (eq != null)
            {
                var item = new GateEventItem
                {
                    GateEventItemId = Guid.NewGuid(),
                    GateEventId = gateEvent.GateEventId,
                    EquipmentId = eq.EquipmentId,
                    Epc = scan.Epc
                };
                _db.GateEventItems.Add(item);

                result.Items.Add(new WorkflowItemResult
                {
                    Epc = scan.Epc,
                    EquipmentName = eq.Name,
                    EquipmentType = eq.EquipmentType!.Name,
                    Status = "Expected" // may be updated below
                });
            }
            else
            {
                result.Items.Add(new WorkflowItemResult
                {
                    Epc = scan.Epc,
                    EquipmentName = null,
                    EquipmentType = null,
                    Status = "Unknown"
                });
            }
        }

        // Compare against template for truck to find missing/extra
        var templates = await _db.TruckEquipmentTemplates
            .Include(t => t.EquipmentType)
            .Where(t => t.TruckId == truckId)
            .ToListAsync();

        var templateExpectedCount = templates.Sum(t => t.RequiredCount);
        var scannedKnownEquipment = result.Items.Where(i => i.Status == "Expected").ToList();

        result.ExpectedCount = templateExpectedCount;
        result.ScannedCount = scannedKnownEquipment.Count;

        // Mark missing based on template vs scanned type counts
        var scannedTypeCounts = scannedKnownEquipment
            .GroupBy(i => i.EquipmentType ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.Count());

        var missingItems = new List<WorkflowItemResult>();

        foreach (var t in templates)
        {
            var key = t.EquipmentType?.Name ?? string.Empty;
            scannedTypeCounts.TryGetValue(key, out var count);
            var missing = t.RequiredCount - count;
            for (int i = 0; i < missing; i++)
            {
                missingItems.Add(new WorkflowItemResult
                {
                    Epc = string.Empty,
                    EquipmentName = null,
                    EquipmentType = t.EquipmentType?.Name,
                    Status = "Missing"
                });
            }
        }

        result.MissingCount = missingItems.Count;
        result.Items.AddRange(missingItems);

        // Extra: scanned equipment whose type is not in template at all
        var templateTypes = templates.Select(t => t.EquipmentType?.Name).Where(n => n != null).ToHashSet();
        foreach (var item in scannedKnownEquipment)
        {
            if (!templateTypes.Contains(item.EquipmentType))
            {
                item.Status = "Extra";
            }
        }

        result.ExtraCount = result.Items.Count(i => i.Status == "Extra" || i.Status == "Unknown");

        // Create alert if missing items
        if (result.MissingCount > 0 && eventType == "Exit")
        {
            _db.Alerts.Add(new Alert
            {
                AlertId = Guid.NewGuid(),
                Timestamp = now,
                Message = $"{result.MissingCount} equipment items missing for Truck {truckId} during {eventType}.",
                Severity = "High",
                Source = "GateEvent",
                IsResolved = false
            });
        }

        await _db.SaveChangesAsync();
        return result;
    }
}
