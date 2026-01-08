using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;

namespace AssetTracking.Rfid.ScanProcessor;

/// <summary>
/// Scan Data Processor
/// -------------------
/// Turns raw RFID scans (RfidScans) into business events for Southern Botanical:
/// - Groups scans into gate sessions (per reader, per time window)
/// - Maps EPCs -> Equipment -> Truck + Driver
/// - Creates GateEvents + GateEventItems
/// - For Exit (Check-Out): detects incomplete kits (missing items at start of day)
/// - For Entry (Check-In): compares with last Exit and detects items not returned
/// - Creates Alerts for missing equipment and late-return trucks
/// </summary>
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

    // Runs every minute
    [Function("ScanDataProcessor")]
    public async Task RunAsync(
        [TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo) // every 1 minute
    {
        var now = DateTime.UtcNow;
        var lookbackMinutes = _config.GetValue<int>("ScanProcessor:LookbackMinutes", 30);
        var sessionWindowSeconds = _config.GetValue<int>("ScanProcessor:SessionWindowSeconds", 20);

        var since = now.AddMinutes(-lookbackMinutes);


        _logger.LogInformation("ScanDataProcessor started at {Time}, lookback: {Since}", now, since);

        // 1) Load unprocessed scans in lookback window
        var scans = await _db.RfidScans
            .Where(s => s.ProcessedAt == null && s.Timestamp >= since)
            .OrderBy(s => s.ReaderId)
            .ThenBy(s => s.Timestamp)
            .ToListAsync();

        if (!scans.Any())
        {
            _logger.LogInformation("No unprocessed scans found.");
            return;
        }

        _logger.LogInformation("Found {Count} unprocessed scans.", scans.Count);

        // 2) Group into sessions: ReaderId + time window
        var sessions = GroupIntoSessions(scans, TimeSpan.FromSeconds(sessionWindowSeconds));

        // Load templates & rules just once
        var templates = await _db.TruckEquipmentTemplates
            .Include(t => t.EquipmentType)
            .ToListAsync();
        var siteIds = scans
    .Select(s => Guid.Parse(s.SiteId))
    .Distinct()
    .ToList();

        var sites = await _db.Sites
            .Where(s => siteIds.Contains(s.SiteId))
            .ToListAsync();

        var SiteId = sites.FirstOrDefault()?.SiteId ?? Guid.Empty;

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

        // Cache EPC -> Equipment
        var epcs = sessions.SelectMany(s => s.Scans).Select(s => s.Epc).Distinct().ToList();

        var trucksByEpc = await _db.Trucks
            .Include(t => t.RfidTag)          // needed to access EPC
            .Where(t => t.RfidTag != null && epcs.Contains(t.RfidTag.Epc))
            .ToListAsync();

        var equipmentByEpc = await _db.Equipment
            .Include(e => e.EquipmentType)
            .Include(e => e.RfidTag)
            .Where(e => e.RfidTag != null && epcs.Contains(e.RfidTag.Epc))
            .ToListAsync();
        GateEvent? gate = null;
        // Process each session -> GateEvent + GateEventItems + Alerts
        gate = await ProcessSessionAsync(
      sessions,
      equipmentByEpc,
      trucksByEpc,
      alertRules,
      now,
      SiteId
  );

        await _db.SaveChangesAsync();

        // Mark scans as processed
        foreach (var scan in scans)
        {
            scan.ProcessedAt = now;
            if (gate != null)
            {
                await HandleTruckEquipmentAssignmentAsync(scan.Epc, gate, now);
            }
        }

        await _db.SaveChangesAsync();

        // Late return / not returned alerts (truck left long ago but no return)
        await ProcessLateReturnAlertsAsync(alertRules, now);

        _logger.LogInformation("ScanDataProcessor finished at {Time}", DateTime.UtcNow);
    }

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
    private async Task<string> AssignEventTypesAsync(ScanSession session, Guid truckId)
    {
        // Get the event type for this single session
        var eventType = await ResolveEventTypeAsync(session, truckId);
        return eventType;
    }


    private async Task<string> ResolveEventTypeAsync(
    ScanSession session,
    Guid truckId)
    {
        // 1️⃣ Try Reader configuration first
        var reader = await _db.Readers
            .FirstOrDefaultAsync(r => r.ReaderId.ToString() == session.ReaderId);

        if (!string.IsNullOrWhiteSpace(reader?.Direction))
            return reader.Direction;

        // 2️⃣ Fallback: infer from last GateEvent
        var lastEvent = await _db.GateEvents
            .Where(g => g.TruckId == truckId)
            .OrderByDescending(g => g.EventTime)
            .Select(g => g.EventType)
            .FirstOrDefaultAsync();

        // 3️⃣ Infer
        return lastEvent == "Entry" ? "Exit" : "Entry";
    }

    /// <summary>
    /// Group raw scans into reader-based time sessions (one truck crossing).
    /// </summary>
    private List<ScanSession> GroupIntoSessions(List<RfidScan> scans, TimeSpan window)
    {
        var sessions = new List<ScanSession>();

        foreach (var group in scans.GroupBy(s => s.ReaderId))
        {
            ScanSession? current = null;

            foreach (var scan in group.OrderBy(s => s.Timestamp))
            {
                if (current == null)
                {
                    current = new ScanSession
                    {
                        ReaderId = group.Key,
                        SiteId = scan.SiteId,
                        Start = scan.Timestamp,
                        End = scan.Timestamp,
                        Scans = new List<RfidScan> { scan }
                    };
                    continue;
                }

                var delta = scan.Timestamp - current.End;
                if (delta <= window)
                {
                    current.Scans.Add(scan);
                    current.End = scan.Timestamp;
                }
                else
                {
                    sessions.Add(current);
                    current = new ScanSession
                    {
                        ReaderId = group.Key,
                        SiteId = scan.SiteId,
                        Start = scan.Timestamp,
                        End = scan.Timestamp,
                        Scans = new List<RfidScan> { scan }
                    };
                }
            }

            if (current != null)
            {
                sessions.Add(current);
            }
        }

        return sessions;
    }


    /// <summary>
    /// Process a single gate session:
    /// - Map EPCs to Equipment
    /// - Determine Truck + Driver
    /// - Create GateEvent + GateEventItems
    /// - Exit (Check-Out): compare against expected template (kit) and raise missing alerts
    /// - Entry (Check-In): compare with last Exit to detect items not returned
    /// </summary>
    private async Task<GateEvent?> ProcessSessionAsync(
       List<ScanSession> session,
      List<Equipment> equipmentByEpc,
      List<Truck> trucksByEpc,
      AlertRules alertRules,
      DateTime now, Guid SiteId)
    {
        // Map ALL EPCs from ALL sessions
        var scannedEpcs = session
      .SelectMany(s => s.Scans)
      .Where(s => !string.IsNullOrWhiteSpace(s.Epc))
      .Select(s => s.Epc.Trim())
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();


        // Map all scanned EPCs to Equipment
        var scannedEquipment = equipmentByEpc
            .Where(e => e.RfidTag != null && scannedEpcs.Contains(e.RfidTag.Epc.Trim()))
            .ToList();



        if (!scannedEquipment.Any())
        {
            // For now, ignore sessions with no known equipment
            _logger.LogInformation("Session at reader {ReaderId} has no known equipment EPCs.", session.First().ReaderId);
            return null;
        }

        // 1) Determine truck by best matching template (Southern Botanical standard kits)
        var scannedTruck = trucksByEpc.FirstOrDefault(t =>
              t.RfidTag != null &&
              scannedEpcs.Contains(t.RfidTag.Epc.Trim()));
        if (scannedTruck.TruckId == Guid.Empty)
        {
            _logger.LogWarning("No matching truck found for session at reader {ReaderId}.", session.First().ReaderId);
            return null;
        }

        // Load truck + driver
        var truck = await _db.Trucks
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.TruckId == scannedTruck.TruckId);

        if (truck == null)
        {
            _logger.LogWarning("Truck {TruckId} not found in DB.", scannedTruck);
            return null;
        }
        var template = await _db.TruckEquipmentTemplates
           .FirstOrDefaultAsync(t => t.TruckId == scannedTruck.TruckId);
        if (template == null)
        {
            _logger.LogWarning("No template found for Truck {TruckId}", scannedTruck.TruckId);
            return null;
        }
        // 2) Determine event type (Entry vs Exit) from Reader.Direction
        // NOTE: RfidScan.ReaderId is string; we assume it stores Reader.ReaderId.ToString()
        var eventType = await AssignEventTypesAsync(session.First(), truck.TruckId);
        if (string.IsNullOrWhiteSpace(eventType))
        {
            // Default to Exit if not configured
            eventType = "Exit";
        }
        var reader = await _db.Readers
            .FirstOrDefaultAsync(r => r.ReaderId.ToString() == session.First().ReaderId);
        // 3) Create GateEvent
        var gateEvent = new GateEvent
        {
            GateEventId = Guid.NewGuid(),
            TruckId = truck.TruckId,
            DriverId = truck.DriverId,
            ReaderId = reader?.ReaderId ?? Guid.Empty,
            EventTime = session.First().End,
            EventType = eventType!,
            Status = "Pending",
            SiteId = SiteId,
            Notes = $"Auto-generated from RfidScans at {session.First().End:u}"
        };

        _db.GateEvents.Add(gateEvent);

        // 4) Create GateEventItems (what we actually saw at the gate)
        foreach (var eq in scannedEquipment)
        {
            var tag = eq.RfidTag!;
            var item = new GateEventItem
            {
                GateEventItemId = Guid.NewGuid(),
                GateEventId = gateEvent.GateEventId,
                EquipmentId = eq.EquipmentId,
                SiteId = SiteId,
                Epc = tag.Epc
            };
            _db.GateEventItems.Add(item);
        }

        // Make EPC sets for comparisons
        var scannedEpcSet = scannedEquipment
            .Where(e => e.RfidTag != null)
            .Select(e => e.RfidTag!.Epc)
            .Distinct()
            .ToHashSet();

        // 5) Exit (Check-Out) logic – detect incomplete kits at morning departure
        if (string.Equals(eventType, "Exit", StringComparison.OrdinalIgnoreCase))
        {
            var expectedEquipmentIds = new List<Guid>
{
    template.EquipmentTypeId
};

            var expectedEpcs = (await _db.Equipment
                    .Include(e => e.RfidTag)
                    .Where(e =>
                        expectedEquipmentIds.Contains(e.EquipmentTypeId) &&
                        e.RfidTag != null)
                    .Select(e => e.RfidTag!.Epc.Trim())
                    .ToListAsync())
                .ToHashSet();

            var missingEpcs = expectedEpcs
                .Except(scannedEpcSet)
                .ToList();

            if (!missingEpcs.Any())
            {
                await _db.SaveChangesAsync();
                return gateEvent;
            }
            var missingEquipmentCosts = await _db.Equipment
               .Include(e => e.RfidTag)
               .Where(e => e.RfidTag != null && missingEpcs.Contains(e.RfidTag.Epc))
               .Select(e => new
               {
                   e.EquipmentId,
                   e.RfidTag!.Epc,
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
            // -------- Alert --------
            if (missingEpcs.Count >= alertRules.MissingItemThreshold)
            {
                _db.Alerts.Add(new Alert
                {
                    AlertId = Guid.NewGuid(),
                    Timestamp = now,
                    Message =
                        $"{missingEpcs.Count} equipment item(s) missing for truck {truck.TruckNumber} during CHECK-OUT at reader {reader?.Name}. Driver: {truck.Driver?.FullName ?? "(unassigned)"}",
                    Severity = matchedSeverity.Code,
                    Source = "GateEvent",
                    SiteId = reader.SiteId,
                    IsResolved = false
                });
            }
           

            // -------- Missing Case --------
            var openStatusId = await _db.MissingEquipmentStatuses
                .Where(s => s.Code == "Open")
                .Select(s => s.StatusId)
                .SingleAsync();

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
                    LastSeenAt = now,
                    StatusId = openStatusId,

                    // ✅ REQUIRED (FK)
                    SeverityId = matchedSeverity.SeverityId,
                    Items = new List<MissingEquipmentCaseItem>()
                };

                _db.MissingEquipmentCases.Add(existingCase);
            }
            else
            {
                existingCase.LastSeenAt = now;
            }
            foreach (var epc in missingEpcs)
            {
                // Skip if the EPC is already in existing case
                if (existingCase.Items.Any(i => i.Epc == epc))
                    continue;

                // Get EquipmentId for this EPC
                var equipmentId = await _db.Equipment
                    .Where(e => e.RfidTag != null && e.RfidTag.Epc == epc)
                    .Select(e => e.EquipmentId)
                    .FirstOrDefaultAsync();

                // Skip if EquipmentId not found
                if (equipmentId == Guid.Empty)
                    continue;

                // Add new item to case
                existingCase.Items.Add(new MissingEquipmentCaseItem
                {
                    MissingEquipmentCaseItemId = Guid.NewGuid(),
                    MissingEquipmentCaseId = existingCase.MissingEquipmentCaseId,
                    EquipmentId = equipmentId,
                    Epc = epc,
                    IsRecovered = false,
                    SiteId = reader.SiteId
                });
            }

            await _db.SaveChangesAsync();
        }
        else if (string.Equals(eventType, "Entry", StringComparison.OrdinalIgnoreCase))
        {
            var lastExit = await _db.GateEvents
                .Include(g => g.Items)
                .Where(g => g.TruckId == truck.TruckId
                            && g.EventType == "Exit"
                            && g.EventTime <= gateEvent.EventTime)
                .OrderByDescending(g => g.EventTime)
                .FirstOrDefaultAsync();

            if (lastExit == null)
            {
                _logger.LogInformation("Entry event for truck {Truck}, no previous Exit found.", truck.TruckNumber);
                return gateEvent;
            }

            var exitMissingEpcs = lastExit.Items
                .Select(i => i.Epc)
                .Distinct()
                .ToHashSet();

            var stillMissingEpcs = exitMissingEpcs.Intersect(scannedEpcSet).ToList();

            if (!stillMissingEpcs.Any())
                return gateEvent;

            var missingEquipNames = await _db.Equipment
                .Include(e => e.RfidTag)
                .Where(e => e.RfidTag != null && stillMissingEpcs.Contains(e.RfidTag.Epc))
                .Select(e => e.Name)
                .ToListAsync();

            var missingList = string.Join(", ", missingEquipNames);

            _db.Alerts.Add(new Alert
            {
                AlertId = Guid.NewGuid(),
                Timestamp = now,
                Message =
                    $"Truck {truck.TruckNumber} (driver: {truck.Driver?.FullName ?? "(unassigned)"}) returned with {missingEquipNames.Count} missing item(s): {missingList}.",
                Severity = "High",
                Source = "GateEvent",
                SiteId = reader.SiteId,
                IsResolved = false
            });
            await _db.SaveChangesAsync();
            await HandleMissingEquipmentAsync(
                truck,
                reader,
                gateEvent,
                stillMissingEpcs.ToHashSet(),
                scannedEpcSet,
                now);

            await _db.SaveChangesAsync();
        }
        // -------------------- 14. Final Save --------------------
        await _db.SaveChangesAsync();
        return gateEvent;

    }
    private async Task HandleMissingEquipmentAsync(
    Truck truck,
    Reader? reader,
    GateEvent gateEvent,
    HashSet<string> stillMissingEpcs,
    HashSet<string> scannedEpcs,
    DateTime now)
    {
        var statuses = await _db.MissingEquipmentStatuses
            .ToDictionaryAsync(s => s.Code, s => s.StatusId);

        var closedStatusId = statuses["Closed"];
        var openStatusId = statuses["Open"];
        var recoveredStatusId = statuses["Recovered"];
        var investigationStatusId = statuses["Investigation"];
        var activeStatusIds = new[] { openStatusId, investigationStatusId };

        // 1️⃣ Load affected cases and items
        var affectedCases = await _db.MissingEquipmentCases
            .Include(c => c.Items)
            .Where(c =>
                activeStatusIds.Contains(c.StatusId) &&
                c.Items.Any(i => !i.IsRecovered && scannedEpcs.Contains(i.Epc)))
            .ToListAsync();

        var existingCase = await _db.MissingEquipmentCases
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c =>
                c.TruckId == truck.TruckId &&
                c.StatusId != closedStatusId);

        // 2️⃣ Update items in memory
        foreach (var c in affectedCases)
        {
            foreach (var i in c.Items.Where(i => !i.IsRecovered && scannedEpcs.Contains(i.Epc)))
            {
                i.IsRecovered = true;
                i.RecoveredAt = now;
            }

            if (c.Items.All(i => i.IsRecovered))
            {
                c.StatusId = closedStatusId;
                c.ClosedAt = now;
                c.LastSeenAt = now;
            }
        }

        if (existingCase != null)
        {
            foreach (var i in existingCase.Items.Where(i => !i.IsRecovered && scannedEpcs.Contains(i.Epc)))
            {
                i.IsRecovered = true;
                i.RecoveredAt = now;
            }
        }



        // 3️⃣ Handle case if nothing missing
        if (!stillMissingEpcs.Any())
        {
            if (existingCase != null)
            {
                existingCase.StatusId =
                    existingCase.Items.All(i => i.IsRecovered)
                        ? closedStatusId
                        : recoveredStatusId;

                existingCase.ClosedAt = now;
                existingCase.LastSeenAt = now;
            }

            await SaveWithRetryAsync();
            return;
        }

        // 4️⃣ Compute severity
        var totalMissingCost = await _db.Equipment
            .Include(e => e.RfidTag)
            .Where(e => e.RfidTag != null && stillMissingEpcs.Contains(e.RfidTag.Epc))
            .SumAsync(e => e.cost);

        var severities = await _db.MissingEquipmentSeverities.ToListAsync();
        var matchedSeverity = severities
            .Select(s =>
            {
                var r = ParseCostRange(s.Cost);
                return new
                {
                    s.SeverityId,
                    s.Code,
                    Min = r.Min.HasValue ? (decimal?)r.Min.Value : null,
                    Max = r.Max.HasValue ? (decimal?)r.Max.Value : null
                };
            })
            .Where(r => totalMissingCost >= r.Min &&
                        (r.Max == null || totalMissingCost <= r.Max.Value))
            .OrderByDescending(r => r.Min)
            .FirstOrDefault()
            ?? throw new Exception($"No severity found for cost {totalMissingCost}");

        // 5️⃣ Create or escalate case
        if (existingCase == null)
        {
            existingCase = new MissingEquipmentCase
            {
                MissingEquipmentCaseId = Guid.NewGuid(),
                TruckId = truck.TruckId,
                DriverId = truck.DriverId,
                SiteId = reader?.SiteId ?? Guid.Empty,
                StatusId = openStatusId,
                SeverityId = matchedSeverity.SeverityId,
                OpenedAt = now,
                LastSeenAt = now,
                Items = new List<MissingEquipmentCaseItem>()
            };

            _db.MissingEquipmentCases.Add(existingCase);
        }
        else
        {
            existingCase.StatusId = investigationStatusId;
            existingCase.LastSeenAt = now;
        }

        // 6️⃣ Load missing equipment once
        var missingEquipments = await _db.Equipment
            .Include(e => e.RfidTag)
            .Where(e => e.RfidTag != null && stillMissingEpcs.Contains(e.RfidTag.Epc))
            .ToListAsync();

        foreach (var eq in missingEquipments)
        {
            if (!existingCase.Items.Any(i => i.Epc == eq.RfidTag!.Epc))
            {
                existingCase.Items.Add(new MissingEquipmentCaseItem
                {
                    MissingEquipmentCaseItemId = Guid.NewGuid(),
                    MissingEquipmentCaseId = existingCase.MissingEquipmentCaseId,
                    EquipmentId = eq.EquipmentId,
                    Epc = eq.RfidTag!.Epc,
                    IsRecovered = false,
                    SiteId = reader?.SiteId ?? Guid.Empty
                });
            }
        }

        // 7️⃣ Add alert if not exists
        var alertExists = await _db.Alerts.AnyAsync(a =>
            a.Source == "MissingEquipment" &&
            a.Message.Contains(existingCase.MissingEquipmentCaseId.ToString()));

        if (!alertExists)
        {
            _db.Alerts.Add(new Alert
            {
                AlertId = Guid.NewGuid(),
                Timestamp = now,
                Severity = matchedSeverity.Code,
                Source = "MissingEquipment",
                Message =
                    $"Case {existingCase.MissingEquipmentCaseId}: Truck {truck.TruckNumber} missing items: " +
                    string.Join(", ", missingEquipments.Select(e => e.Name)),
                IsResolved = false,
                SiteId = reader?.SiteId ?? Guid.Empty
            });
        }

        // 8️⃣ Save **once** with retry
        await SaveWithRetryAsync();

        async Task SaveWithRetryAsync()
        {
            int attempts = 0;
            bool saved = false;

            while (!saved && attempts < 3)
            {
                try
                {
                    await _db.SaveChangesAsync();
                    saved = true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    attempts++;
                    foreach (var entry in ex.Entries)
                    {
                        await entry.ReloadAsync(); // reload latest values
                    }
                    _logger.LogWarning("Concurrency conflict, retrying SaveChangesAsync.");
                }
            }

            if (!saved)
                throw new Exception("Failed to save after 3 concurrency retries");
        }
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
    private async Task<HashSet<string>> GetExpectedEpcsAsync(Guid truckId)
    {
        var rows = await (
            from t in _db.TruckEquipmentTemplates
            join e in _db.Equipment
                on t.EquipmentTypeId equals e.EquipmentTypeId
            where t.TruckId == truckId
                  && e.RfidTag != null
            select new
            {
                e.RfidTag!.Epc,
                t.RequiredCount
            }
        ).ToListAsync();

        return rows
            .SelectMany(x => Enumerable.Repeat(x.Epc, x.RequiredCount))
            .ToHashSet();
    }
    /// <summary>
    /// Choose best matching truck based on how many equipment types match its template.
    /// </summary>
    //private Guid FindBestMatchingTruck(
    //    List<Equipment> scannedEquipment,
    //    List<TruckEquipmentTemplate> templates)
    //{
    //    if (!templates.Any())
    //        return Guid.Empty;

    //    var typeCounts = scannedEquipment
    //        .GroupBy(e => e.EquipmentTypeId)
    //        .ToDictionary(g => g.Key, g => g.Count());

    //    var scores = templates
    //        .GroupBy(t => t.TruckId)
    //        .Select(g =>
    //        {
    //            var truckId = g.Key;
    //            int score = 0;
    //            foreach (var t in g)
    //            {
    //                typeCounts.TryGetValue(t.EquipmentTypeId, out var scannedCount);
    //                // score = min(scannedCount, requiredCount) so better matches rank higher
    //                score += Math.Min(scannedCount, t.RequiredCount);
    //            }
    //            return new { TruckId = truckId, Score = score };
    //        })
    //        .OrderByDescending(x => x.Score)
    //        .ToList();

    //    var best = scores.FirstOrDefault();
    //    if (best == null || best.Score == 0)
    //        return Guid.Empty;

    //    return best.TruckId;
    //}

    /// <summary>
    /// Late-return alerts: trucks that exited long ago but have no newer Entry event.
    /// </summary>
    private async Task ProcessLateReturnAlertsAsync(AlertRules alertRules, DateTime now)
    {
        var overdueMinutes = alertRules.OverdueMinutes;
        if (overdueMinutes <= 0) return;

        var cutoff = now.AddMinutes(-overdueMinutes);

        // For each truck, find last gate event
        var lastEvents = await _db.GateEvents
            .GroupBy(g => g.TruckId)
            .Select(g => g.OrderByDescending(x => x.EventTime).First())
            .ToListAsync();

        foreach (var ge in lastEvents)
        {
            if (ge.EventType == "Exit" && ge.EventTime <= cutoff)
            {
                // Check if an existing late-return alert exists
                var existing = await _db.Alerts.FirstOrDefaultAsync(a =>
                    a.Source == "LateReturn" &&
                    a.IsResolved == false &&
                    a.Message.Contains(ge.TruckId.ToString()));

                if (existing != null) continue;

                var alert = new Alert
                {
                    AlertId = Guid.NewGuid(),
                    Timestamp = now,
                    Message = $"Truck {ge.TruckId} has not returned for more than {overdueMinutes} minutes (last exit at {ge.EventTime:u}).",
                    Severity = "Medium",
                    Source = "LateReturn",
                    IsResolved = false,
                    SiteId = ge?.SiteId ?? Guid.Empty
                };
                _db.Alerts.Add(alert);
            }
        }

        await _db.SaveChangesAsync();
    }

    private class ScanSession
    {
        public string ReaderId { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public List<RfidScan> Scans { get; set; } = new();
    }
}
