using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AssetTracking.Rfid.ScanProcessor;

public class ScanDataProcessorFunction
{
  private readonly ILogger<ScanDataProcessorFunction> _logger;
  private readonly AppDbContext _db;
  private readonly IConfiguration _config;

  public ScanDataProcessorFunction(
      ILogger<ScanDataProcessorFunction> logger,
      AppDbContext db,
      IConfiguration config)
  {
    _logger = logger;
    _db = db;
    _config = config;
  }

  [Function("ScanDataProcessor")]
  public async Task RunAsync([TimerTrigger("0 */1 * * * *")] TimerInfo timer)
  {
    var now = DateTime.UtcNow;
    var lookbackMinutes = _config.GetValue<int>("ScanProcessor:LookbackMinutes", 30);
    var since = now.AddMinutes(-lookbackMinutes);

    var scans = await _db.RfidScans
        .Where(s => s.ProcessedAt == null && s.Timestamp >= since)
        .OrderBy(s => s.ReaderId)
        .ThenBy(s => s.Timestamp)
        .ToListAsync();

    if (!scans.Any())
      return;

    var deduped = scans
        .GroupBy(s => new { s.ReaderId, Epc = s.Epc!.Trim() })
        .Select(g => g.OrderByDescending(x => x.Timestamp).First())
        .ToList();
    var alertRules = await _db.AlertRules.FirstOrDefaultAsync()
           ?? new AlertRules
           {
             AlertRulesId = Guid.NewGuid(),
             MissingItemThreshold = 1,
             OverdueMinutes = 60,
             NotifyEmail = true,
             NotifySms = true,
             NotifyPush = false
           };

    if (alertRules.AlertRulesId == Guid.Empty)
    {
      alertRules.AlertRulesId = Guid.NewGuid();
      _db.AlertRules.Add(alertRules);
    }

    var sessions = GroupIntoSessions(deduped, TimeSpan.FromMinutes(10));
    GateEvent? gate = null;

    foreach (var session in sessions)
    {



      gate = await ProcessSingleSessionAsync(session, now);
    }

    foreach (var scan in scans)
    {
      scan.ProcessedAt = now;

      if (gate != null)
      {
        await HandleTruckEquipmentAssignmentAsync(scan.Epc, gate, now);
      }
    }

    await _db.SaveChangesAsync();
  }

  // ========================= SESSION PROCESSING =========================

  private async Task<GateEvent?> ProcessSingleSessionAsync(ScanSession session, DateTime now)
  {
    var scannedEpcs = session.Scans
        .Select(s => s.Epc?.Trim())
        .Where(e => !string.IsNullOrWhiteSpace(e))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    if (!scannedEpcs.Any())
      return null;

    var truck = await _db.Trucks
        .Include(t => t.Driver)
        .Include(t => t.RfidTag)
        .FirstOrDefaultAsync(t =>
            t.RfidTag != null &&
            scannedEpcs.Contains(t.RfidTag.TagName));

    if (truck == null)
      return null;

    var reader = await _db.Readers
      .FirstOrDefaultAsync(r => r.ReaderId.ToString() == session.ReaderId);

    if (reader == null)
      return null;

    var eventType = ResolveEventType(reader, truck.TruckId);
    var siteId = reader.SiteId;
    var eventTime = session.End;

    var todayStart = eventTime.Date;

    // 🔹 Last event for THIS Truck + Site + Today
    var lastGateEvent = await _db.GateEvents
        .Where(g =>
            g.TruckId == truck.TruckId &&
            g.SiteId == siteId &&
            g.EventTime >= todayStart)
        .OrderByDescending(g => g.EventTime)
        .FirstOrDefaultAsync();

    // 🔒 ENTRY / EXIT STATE VALIDATION
    if (eventType == "Entry")
    {
      // Block ENTRY if last event was ENTRY
      if (lastGateEvent != null && lastGateEvent.EventType == "Entry")
      {
        _logger.LogInformation(
            "ENTRY blocked: Truck already inside | Truck={TruckId}, Site={SiteId}",
            truck.TruckId, siteId);

        return null;
      }
    }
    else if (eventType == "Exit")
    {
      // Block EXIT if no ENTRY today
      if (lastGateEvent == null)
      {
        _logger.LogInformation(
            "EXIT blocked: No ENTRY found today | Truck={TruckId}, Site={SiteId}",
            truck.TruckId, siteId);

        return null;
      }

      // Block EXIT if last event already EXIT
      if (lastGateEvent.EventType == "Exit")
      {
        _logger.LogInformation(
            "EXIT blocked: Already exited | Truck={TruckId}, Site={SiteId}",
            truck.TruckId, siteId);

        return null;
      }
    }

    // ⏱ 120-minute duplicate protection
    if (lastGateEvent != null &&
        lastGateEvent.EventType == eventType &&
        (eventTime - lastGateEvent.EventTime).TotalMinutes < 120)
    {
      _logger.LogInformation(
          "Duplicate {EventType} ignored within 120 mins | Truck={TruckId}, Site={SiteId}",
          eventType, truck.TruckId, siteId);

      return null;
    }

    // ✅ Create GateEvent
    var gateEvent = new GateEvent
    {
      GateEventId = Guid.NewGuid(),
      TruckId = truck.TruckId,
      DriverId = truck.DriverId,
      ReaderId = reader.ReaderId,
      SiteId = siteId,
      EventTime = eventTime,
      EventType = eventType,
      Status = "Completed"
    };

    _db.GateEvents.Add(gateEvent);

    // 🔹 Add equipment
    var equipments = await _db.Equipment
        .Include(e => e.RfidTag)
        .Where(e =>
            e.RfidTag != null &&
            scannedEpcs.Contains(e.RfidTag.TagName))
        .ToListAsync();

    var gateEventItems = new List<GateEventItem>();

    foreach (var eq in equipments)
    {
      var item = new GateEventItem
      {
        GateEventItemId = Guid.NewGuid(),
        GateEventId = gateEvent.GateEventId,
        EquipmentId = eq.EquipmentId,
        Epc = eq.RfidTag!.TagName,
        SiteId = siteId
      };

      gateEventItems.Add(item);
      _db.GateEventItems.Add(item);
    }
    if (eventType == "Entry")
      await HandleEntryAsync(truck, scannedEpcs, reader, now);

    if (eventType == "Exit")
      await HandleExitAsync(truck, scannedEpcs, reader, now, gateEvent, gateEventItems);

    await _db.SaveChangesAsync();
    return gateEvent;
  }

  // ========================= ENTRY =========================

  private async Task HandleEntryAsync(
      Truck truck,
      HashSet<string> scannedEpcs,
      Reader reader,
      DateTime now)
  {
    var openCases = await _db.MissingEquipmentCases
        .Include(c => c.Items)
        .Where(c => c.TruckId == truck.TruckId && c.ClosedAt == null)
        .ToListAsync();

    foreach (var c in openCases)
    {
      foreach (var item in c.Items.Where(i => scannedEpcs.Contains(i.Epc)))
      {
        item.IsRecovered = true;
        item.RecoveredAt = now;
      }

      if (c.Items.All(i => i.IsRecovered))
      {
        c.ClosedAt = now;
        c.StatusId = await GetStatusId("Closed");
      }
    }
  }

  // ========================= EXIT =========================

  private async Task HandleExitAsync(
      Truck truck,
      HashSet<string> scannedEpcs,
      Reader reader,
      DateTime now, GateEvent gateEventId,
    List<GateEventItem> gateEventItems)
  {
    var expectedEpcs = await GetExpectedEpcsAsync(truck.TruckId);

    var missingEpcs = expectedEpcs
        .Except(scannedEpcs, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (!missingEpcs.Any())
      return;
    var missingEquipmentCosts = await _db.Equipment
            .Include(e => e.RfidTag)
            .Where(e => e.RfidTag != null && missingEpcs.Contains(e.RfidTag.TagName))
            .Select(e => new
            {
              e.EquipmentId,
              e.RfidTag!.TagName,
              Cost = e.cost
            })
            .ToListAsync();
    var totalMissingCost = missingEquipmentCosts.Sum(x => x.Cost);

    var severities = await _db.MissingEquipmentSeverities
.Select(s => new
{
s.SeverityId,
s.Code,
s.Cost // range string
})
.ToListAsync();

    var matchedSeverity = severities
        .Select(s =>
        {
          var range = ParseCostRange(s.Cost);

          return new
          {
            s.SeverityId,
            s.Code,
            Min = range.Min.HasValue ? (decimal?)range.Min.Value : null,
            Max = range.Max.HasValue ? (decimal?)range.Max.Value : null
          };
        })
        .Where(r =>
            totalMissingCost >= r.Min &&
            (r.Max == null || totalMissingCost <= r.Max))
        .OrderByDescending(r => r.Min)
        .FirstOrDefault();

    if (matchedSeverity == null)
    {
      throw new Exception($"No severity found for cost {totalMissingCost}");
    }

    var severityId = matchedSeverity.SeverityId;

    var openStatusId = await GetStatusId("Open");

    var existingCase = await _db.MissingEquipmentCases
     .Include(c => c.Items)
     .FirstOrDefaultAsync(c =>
         c.TruckId == truck.TruckId &&
         c.StatusId == openStatusId);

    if (existingCase == null)
    {
      existingCase = new MissingEquipmentCase
      {
        MissingEquipmentCaseId = Guid.NewGuid(),
        TruckId = truck.TruckId,
        DriverId = truck.DriverId,
        SiteId = reader.SiteId,
        OpenedAt = now,
        StatusId = openStatusId,
        SeverityId = matchedSeverity.SeverityId,
        Items = new List<MissingEquipmentCaseItem>()
      };

      _db.MissingEquipmentCases.Add(existingCase);
    }

    foreach (var epc in missingEpcs)
    {
      // 🔒 Prevent duplicate items
      if (existingCase.Items.Any(i => i.Epc == epc))
        continue;

      // ✅ Load full equipment (needed for Name)
      var equipment = await _db.Equipment
          .Include(e => e.RfidTag)
          .FirstOrDefaultAsync(e =>
              e.RfidTag != null &&
              e.RfidTag.TagName == epc);

      if (equipment == null)
        continue;

      // 🆕 Create case item
      var caseItem = new MissingEquipmentCaseItem
      {
        MissingEquipmentCaseItemId = Guid.NewGuid(),
        MissingEquipmentCaseId = existingCase.MissingEquipmentCaseId,
        EquipmentId = equipment.EquipmentId,
        Epc = epc,
        IsRecovered = false,
        SiteId = reader.SiteId
      };

      existingCase.Items.Add(caseItem);

      // 🚨 Emit ALERT only once per CASE + EPC
      var alertExists = await _db.Alerts.AnyAsync(a =>
          a.Source == "MissingEquipment" &&
          a.Message.Contains(existingCase.MissingEquipmentCaseId.ToString()) &&
          a.Message.Contains(epc));

      if (!alertExists)
      {
        _db.Alerts.Add(new Alert
        {
          AlertId = Guid.NewGuid(),
          Timestamp = now,
          Severity = "High",
          Source = "MissingEquipment",
          Message =
                $"Case {existingCase.MissingEquipmentCaseId}: " +
                $"Truck {truck.TruckNumber} missing equipment {equipment.Name} (EPC: {epc})",
          IsResolved = false,
          SiteId = reader.SiteId
        });
      }
    }


    // ✅ SINGLE SaveChanges
    await _db.SaveChangesAsync();

  }
  private static (double? Min, double? Max) ParseCostRange(string costRange)
  {
    costRange = costRange.Replace("$", "").Trim();

    // Handle "2001+"
    if (costRange.EndsWith("+"))
    {
      var min = double.Parse(costRange.Replace("+", ""));
      return (min, null);
    }

    // Handle "1-100"
    var parts = costRange.Split('-');
    return (
        double.Parse(parts[0]),
        double.Parse(parts[1])
    );
  }

  private static decimal ParseCost(string cost)
  {
    if (string.IsNullOrWhiteSpace(cost))
      return 0;

    cost = cost.Replace("$", "")
               .Replace(",", "")
               .Trim();

    return decimal.TryParse(cost, out var value) ? value : 0;
  }

  // ========================= HELPERS =========================
  private async Task HandleTruckEquipmentAssignmentAsync(
  string epc,
  GateEvent gate,
  DateTime now)
  {
    // Load GateEventItems to know which equipment was scanned
    var gateEventItems = await _db.GateEventItems
        .Where(i => i.GateEventId == gate.GateEventId)
        .ToListAsync();

    if (!gateEventItems.Any())
      return;

    if (string.Equals(gate.EventType, "Entry", StringComparison.OrdinalIgnoreCase))
    {
      // ENTRY → Assign equipment to truck
      foreach (var item in gateEventItems)
      {
        var existingAssignment = await _db.TruckEquipmentAssignments
            .FirstOrDefaultAsync(a =>
                a.TruckId == gate.TruckId &&
                a.EquipmentId == item.EquipmentId &&
                a.ReturnedAt == null);

        // Prevent duplicate active assignment
        if (existingAssignment != null)
          continue;

        var assignment = new TruckEquipmentAssignment
        {
          AssignmentId = Guid.NewGuid(),
          TruckId = gate.TruckId,
          EquipmentId = item.EquipmentId,
          AssignedAt = now,
          ReturnedAt = null,
          SiteId = gate.SiteId
        };

        _db.TruckEquipmentAssignments.Add(assignment);
      }
      await _db.SaveChangesAsync();
    }
    else if (string.Equals(gate.EventType, "Exit", StringComparison.OrdinalIgnoreCase))
    {
      // EXIT → Return equipment
      foreach (var item in gateEventItems)
      {
        var assignment = await _db.TruckEquipmentAssignments
            .FirstOrDefaultAsync(a =>
                a.TruckId == gate.TruckId &&
                a.EquipmentId == item.EquipmentId &&
                a.ReturnedAt == null);

        if (assignment != null)
        {
          assignment.ReturnedAt = now;
        }
      }
    }

    await _db.SaveChangesAsync();
  }

  private string ResolveEventType(Reader reader, Guid truckId)
  {
    var direction = reader.Direction?.Trim().ToUpperInvariant();

    // ENTRY-only reader
    if (direction == "ENTRY")
      return "Entry";

    // EXIT-only reader
    if (direction == "EXIT")
      return "Exit";

    // BOTH-direction reader → decide from history
    if (direction == "BOTH")
    {
      var lastEvent = _db.GateEvents
          .Where(g => g.TruckId == truckId)
          .OrderByDescending(g => g.EventTime)
          .Select(g => g.EventType)
          .FirstOrDefault();

      // First scan ever → default Entry
      if (lastEvent == null)
        return "Entry";

      // Toggle state
      return lastEvent == "Entry" ? "Exit" : "Entry";
    }

    // Safety fallback
    return "Entry";
  }



  private async Task<int> GetStatusId(string code)
  {
    return await _db.MissingEquipmentStatuses
        .Where(s => s.Code == code)
        .Select(s => s.StatusId)
        .SingleAsync();
  }

  // ✅ FIXED METHOD
  private async Task<HashSet<string>> GetExpectedEpcsAsync(Guid truckId)
  {
    // 1️⃣ Find last ENTRY gate event for today
    var today = DateTime.UtcNow.Date;

    var lastEntryGateEventId = await _db.GateEvents
        .Where(g =>
            g.TruckId == truckId &&
            g.EventType == "Entry" &&
            g.EventTime >= today)
        .OrderByDescending(g => g.EventTime)
        .Select(g => g.GateEventId)
        .FirstOrDefaultAsync();

    if (lastEntryGateEventId == Guid.Empty)
    {
      // No entry today → no expected EPCs
      return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    // 2️⃣ Get EPCs scanned in that ENTRY gate event
    var entryEpcs = await _db.GateEventItems
        .Where(i =>
            i.GateEventId == lastEntryGateEventId &&
            i.Epc != null)
        .Select(i => i.Epc!)
        .ToListAsync();

    var entryEpcSet = entryEpcs
        .Select(e => e.Trim())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    if (!entryEpcSet.Any())
    {
      return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    // 3️⃣ Load expected EPC templates for the truck
    var raw = await _db.TruckEquipmentTemplates
        .Join(_db.Equipment,
            t => t.EquipmentTypeId,
            e => e.EquipmentTypeId,
            (t, e) => new
            {
              t.TruckId,
              t.RequiredCount,
              TagName = e.RfidTag != null ? e.RfidTag.TagName : null
            })
        .Where(x =>
            x.TruckId == truckId &&
            x.RequiredCount > 0 &&
            x.TagName != null)
        .ToListAsync();

    // 4️⃣ Keep ONLY EPCs that were scanned at ENTRY
    return raw
        .Where(x => entryEpcSet.Contains(x.TagName!.Trim()))
        .SelectMany(x =>
            Enumerable.Repeat(x.TagName!.Trim(), x.RequiredCount))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
  }


  private List<ScanSession> GroupIntoSessions(
      List<RfidScan> scans,
      TimeSpan window)
  {
    var sessions = new List<ScanSession>();

    foreach (var group in scans.GroupBy(s => s.ReaderId))
    {
      ScanSession? current = null;

      foreach (var scan in group.OrderBy(s => s.Timestamp))
      {
        if (current == null || scan.Timestamp - current.End > window)
        {
          current = new ScanSession
          {
            ReaderId = group.Key,
            SiteId = scan.SiteId,
            Start = scan.Timestamp,
            End = scan.Timestamp,
            Scans = new List<RfidScan>()
          };
          sessions.Add(current);
        }

        current.Scans.Add(scan);
        current.End = scan.Timestamp;
      }
    }

    return sessions;
  }

  private class ScanSession
  {
    public string ReaderId { get; set; } = "";
    public string SiteId { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public List<RfidScan> Scans { get; set; } = new();
  }
}
