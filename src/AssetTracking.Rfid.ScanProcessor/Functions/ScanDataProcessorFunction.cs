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

        //find truck maching by rfid tags


        // 2) Group into sessions: ReaderId + time window
        var sessions = GroupIntoSessions(scans, TimeSpan.FromSeconds(sessionWindowSeconds));

        //// Load templates & rules just once
        //var templates = await _db.TruckEquipmentTemplates
        //    .Include(t => t.EquipmentType)
        //    .ToListAsync();

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
        foreach (var session in sessions)
        {
             gate = await ProcessSessionAsync(session, equipmentByEpc, trucksByEpc, alertRules, now);

        }
        await _db.SaveChangesAsync();
        // Mark scans as processed
        foreach (var scan in scans)
        {
            scan.ProcessedAt = now;
            if (gate != null)
            {
                await HandleTruckEquipmentAssignmentAsync(scan.Epc,gate, now);
            }

        }

        await _db.SaveChangesAsync();

        // Late return / not returned alerts (truck left long ago but no return)
        await ProcessLateReturnAlertsAsync(alertRules, now);
        
        await _db.SaveChangesAsync();


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
                    ReturnedAt = null
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

    /// <summary>
    /// Group raw scans into reader-based time sessions (one truck crossing).
    /// </summary>
    private List<ScanSession> GroupIntoSessions(
        List<RfidScan> scans,
        TimeSpan window)
    {
        var sessions = new List<ScanSession>();

        // Group first by reader
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
    private static (decimal Min, decimal? Max) ParseCostRange(string costRange)
    {
        costRange = costRange.Replace("$", "").Trim();

        // Example: "2001+"
        if (costRange.EndsWith("+"))
        {
            var min = decimal.Parse(costRange.Replace("+", ""));
            return (min, null);
        }

        // Example: "1-100"
        var parts = costRange.Split('-');
        return (
            decimal.Parse(parts[0]),
            decimal.Parse(parts[1])
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

    private async Task<GateEvent?> ProcessSessionAsync(
     ScanSession session,
     List<Equipment> equipmentByEpc,
     List<Truck> trucksByEpc,
     AlertRules alertRules,
     DateTime now)
    {
        // Map EPCs -> Equipment
        var scannedEpcs = session.Scans.Select(s => s.Epc).Distinct().ToList();
        var scannedEquipment = equipmentByEpc
            .Where(e => e.RfidTag != null && scannedEpcs.Contains(e.RfidTag.Epc))
            .ToList();

        var scannedTruck = trucksByEpc
            .Where(e => e.RfidTag != null && scannedEpcs.Contains(e.RfidTag.Epc))
            .ToList();

        if (!scannedEquipment.Any())
        {
            // For now, ignore sessions with no known equipment
            _logger.LogInformation("Session at reader {ReaderId} has no known equipment EPCs.", session.ReaderId);
            return null;
        }

        var scannedTruckIds = scannedTruck
            .Select(t => t.TruckId)
            .Distinct()
            .ToList();

        // Load templates & rules just once
        var templates = await _db.TruckEquipmentTemplates
            .Where(t => scannedTruckIds.Contains(t.TruckId))
            .ToListAsync();

        // 1) Determine truck by best matching template (Southern Botanical standard kits)
        var bestTruckId = FindBestMatchingTruck(scannedEquipment, templates);
        if (bestTruckId == Guid.Empty)
        {
            _logger.LogWarning("No matching truck found for session at reader {ReaderId}.", session.ReaderId);
            return null;
        }

        // Load truck + driver
        var truck = await _db.Trucks
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.TruckId == bestTruckId);

        if (truck == null)
        {
            _logger.LogWarning("Truck {TruckId} not found in DB.", bestTruckId);
            return null;
        }

        // 2) Determine event type (Entry vs Exit) from Reader.Direction
        // NOTE: RfidScan.ReaderId is string; we assume it stores Reader.ReaderId.ToString()
        var readerIdString = session.ReaderId?.Trim();

        if (!Guid.TryParse(readerIdString, out var readerId))
        {
            throw new InvalidOperationException($"Invalid ReaderId: {readerIdString}");
        }

        var reader = await _db.Readers.FirstOrDefaultAsync(r => r.ReaderId == readerId);

        if (reader == null)
        {
            throw new InvalidOperationException($"Reader not found: {readerId}");
        }

        var eventType = reader?.Direction;
        if (string.IsNullOrWhiteSpace(eventType))
        {
            // Default to Exit if not configured
            eventType = "Exit";
        }

        // 3) Create GateEvent
        var gateEvent = new GateEvent
        {
            GateEventId = Guid.NewGuid(),
            TruckId = truck.TruckId,
            DriverId = truck.DriverId,
            ReaderId = reader?.ReaderId ?? Guid.Empty,
            EventTime = session.End,
            EventType = eventType!,
            Status = "Pending",
            Notes = $"Auto-generated from RfidScans at {session.End:u}"
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
            // ---------- Status IDs ----------
            var closedStatusId = await _db.MissingEquipmentStatuses
                .Where(s => s.Code == "Closed")
                .Select(s => s.StatusId)
                .SingleAsync();

            var openStatusId = await _db.MissingEquipmentStatuses
                .Where(s => s.Code == "Open")
                .Select(s => s.StatusId)
                .SingleAsync();

            // ---------- Existing open case ----------
            var existingCase = await _db.MissingEquipmentCases
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c =>
                    c.TruckId == truck.TruckId &&
                    c.StatusId != closedStatusId);

            // ---------- Expected vs scanned ----------
            var expectedEpcs = await GetExpectedEpcsAsync(truck.TruckId);

            var missingEpcs = expectedEpcs
                .Except(scannedEpcSet)
                .ToHashSet();

            if (!missingEpcs.Any())
                return gateEvent;

            // ---------- Load missing equipment cost ----------
            var missingEquipmentCosts = await _db.Equipment
                .Include(e => e.RfidTag)
                .Where(e => e.RfidTag != null && missingEpcs.Contains(e.RfidTag.Epc))
                .Select(e => new
                {
                    e.RfidTag!.Epc,
                    Cost = e.cost
                })
                .ToListAsync();

            var totalMissingCost = missingEquipmentCosts.Sum(x => x.Cost);

            // ---------- Resolve severity ----------
            var severities = await _db.MissingEquipmentSeverities 
      .Select(s => new
      {
          s.SeverityId,
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
                        range.Min,
                        range.Max
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

            // ---------- Create or update case ----------
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
                    SeverityId = matchedSeverity.SeverityId,
                    Items = new List<MissingEquipmentCaseItem>()
                };

                _db.MissingEquipmentCases.Add(existingCase);
            }
            else
            {
                existingCase.LastSeenAt = now;
                existingCase.SeverityId = matchedSeverity.SeverityId;
            }

            foreach (var epc in missingEpcs)
            {
                if (!existingCase.Items.Any(i => i.Epc == epc))
                {
                    existingCase.Items.Add(new MissingEquipmentCaseItem
                    {
                        MissingEquipmentCaseItemId = Guid.NewGuid(),
                        MissingEquipmentCaseId = existingCase.MissingEquipmentCaseId,
                        Epc = epc,
                        IsRecovered = false
                    });
                }
            }

            return gateEvent;
        }

        // 6) Entry (Check-In) logic – compare with last Exit to find items not returned
        else if (string.Equals(eventType, "Entry", StringComparison.OrdinalIgnoreCase))
        {
            // Find last Exit event for this truck BEFORE this Entry
            var lastExit = await _db.GateEvents
                .Include(g => g.Items)
                .Where(g => g.TruckId == truck.TruckId
                            && g.EventType == "Exit"
                            && g.EventTime <= gateEvent.EventTime)
                .OrderByDescending(g => g.EventTime)
                .FirstOrDefaultAsync();

            if (lastExit != null)
            {
                var exitEpcs = lastExit.Items
                    .Select(i => i.Epc)
                    .Distinct()
                    .ToHashSet();

                var stillMissingEpcs = exitEpcs.Except(scannedEpcSet).ToList();

                if (stillMissingEpcs.Any())
                {
                    var missingEquipNames = await _db.Equipment
                        .Include(e => e.RfidTag)
                        .Where(e => e.RfidTag != null && stillMissingEpcs.Contains(e.RfidTag.Epc))
                        .Select(e => e.Name)
                        .ToListAsync();

                    var missingList = string.Join(", ", missingEquipNames);

                    var alert = new Alert
                    {
                        AlertId = Guid.NewGuid(),
                        Timestamp = now,
                        Message =
                            $"Truck {truck.TruckNumber} (driver: {truck.Driver?.FullName ?? "(unassigned)"}) returned with {missingEquipNames.Count} missing item(s): {missingList}.",
                        Severity = "High",
                        Source = "GateEvent",
                        IsResolved = false
                    };
                    _db.Alerts.Add(alert);

                    _logger.LogWarning(
                        "Entry event: truck {Truck}, driver {Driver}, missing {MissingCount} item(s): {MissingList}.",
                        truck.TruckNumber,
                        truck.Driver?.FullName,
                        missingEquipNames.Count,
                        missingList);
                }
                await HandleMissingEquipmentAsync(truck, reader, gateEvent, stillMissingEpcs.ToHashSet(), scannedEpcSet, now);

                return gateEvent;
            }
            else
            {
                _logger.LogInformation(
                    "Entry event for truck {TruckNumber}, but no previous Exit event found.",
                    truck.TruckNumber);
            }
            return gateEvent;
        }

        // ✅ REQUIRED: covers Exit path and future event types
        return gateEvent;
    }

    private async Task HandleMissingEquipmentAsync(Truck truck, Reader? reader, GateEvent gateEvent, HashSet<string> stillMissingEpcs, HashSet<string> scannedEpcs, DateTime now)
    {
        var closedStatusId = await _db.MissingEquipmentStatuses
             .Where(s => s.Code == "Closed")
             .Select(s => s.StatusId)
             .SingleAsync();

        var openStatusId = await _db.MissingEquipmentStatuses
            .Where(s => s.Code == "Open")
            .Select(s => s.StatusId)
            .SingleAsync();

        var recoveredStatusId = await _db.MissingEquipmentStatuses
           .Where(s => s.Code == "Recovered")
           .Select(s => s.StatusId)
           .SingleAsync();

        var investigationStatusId = await _db.MissingEquipmentStatuses
           .Where(s => s.Code == "Investigation")
           .Select(s => s.StatusId)
           .SingleAsync();

        // 1️⃣ Load existing open case (ONE per truck)
        var existingCase = await _db.MissingEquipmentCases 
            .Include(c => c.Items) 
            .FirstOrDefaultAsync(c => c.TruckId == truck.TruckId && c.StatusId != closedStatusId); 
        // 2️⃣ AUTO-RECOVER items that reappear
        if (existingCase != null) {
            foreach (var item in existingCase.Items 
                .Where(i => !i.IsRecovered && scannedEpcs.Contains(i.Epc))) {
                item.IsRecovered = true; item.RecoveredAt = now; } } 
        // 3️⃣ If NOTHING missing → close case
        if (!stillMissingEpcs.Any()) { if (existingCase != null) { 
                if (existingCase.Items.All(i => i.IsRecovered)) { 
                    existingCase.StatusId = closedStatusId; existingCase.ClosedAt = now; }
                else { existingCase.StatusId = recoveredStatusId; } existingCase.LastSeenAt = now; }
            await _db.SaveChangesAsync();
            return; }
        var missingEquipmentCosts = await _db.Equipment
               .Include(e => e.RfidTag)
               .Where(e => e.RfidTag != null && stillMissingEpcs.Contains(e.RfidTag.Epc))
               .Select(e => new
               {
                   e.RfidTag!.Epc,
                   Cost = e.cost
               })
               .ToListAsync();

        var totalMissingCost = missingEquipmentCosts.Sum(x => x.Cost);

        // ---------- Resolve severity ----------
        var severities = await _db.MissingEquipmentSeverities
  .Select(s => new
  {
      s.SeverityId,
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
                    range.Min,
                    range.Max
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
        // 4️⃣ Create case if NOT exists
        if (existingCase == null) { 
            existingCase = new MissingEquipmentCase 
            {
                MissingEquipmentCaseId = Guid.NewGuid(),
                TruckId = truck.TruckId,
                DriverId = truck.DriverId, 
                SiteId = reader?.SiteId ?? Guid.Empty,
                StatusId = openStatusId,
                SeverityId = matchedSeverity.SeverityId,
                OpenedAt = now,
                LastSeenAt = now 
            }; 
            _db.MissingEquipmentCases.Add(existingCase); } else { 
            // Escalate state
            existingCase.StatusId = investigationStatusId; existingCase.LastSeenAt = now; } 
        // 5️⃣ Load missing equipment
        var missingEquipments = await _db.Equipment 
            .Include(e => e.RfidTag) 
            .Where(e => e.RfidTag != null && stillMissingEpcs.Contains(e.RfidTag.Epc)) 
            .ToListAsync(); 
        // 6️⃣ Add MissingEquipmentCaseItems (NO DUPLICATES)
        foreach (var eq in missingEquipments) { 
            if (!existingCase.Items.Any(i => i.Epc == eq.RfidTag!.Epc)) { 
                existingCase.Items.Add(new MissingEquipmentCaseItem 
                { MissingEquipmentCaseItemId = Guid.NewGuid(),
                    MissingEquipmentCaseId = existingCase.MissingEquipmentCaseId,
                    EquipmentId = eq.EquipmentId,
                    Epc = eq.RfidTag!.Epc,
                    IsRecovered = false }); } } 
        // 7️⃣ Emit ALERT only ONCE
        var alertExists = await _db.Alerts
            .AnyAsync(a => a.Source == "MissingEquipment" && a.Message
            .Contains(existingCase.MissingEquipmentCaseId.ToString()));
        if (!alertExists) { 
            var names = string.Join(", ", missingEquipments.Select(e => e.Name)); 
            _db.Alerts.Add(new Alert { 
                AlertId = Guid.NewGuid(),
                Timestamp = now, 
                Severity = "High",
                Source = "MissingEquipment",
                Message = $"Case {existingCase.MissingEquipmentCaseId}: Truck {truck.TruckNumber} missing items: {names}",
                IsResolved = false }); 
        } await _db.SaveChangesAsync();
    }


    /// <summary>
    /// Choose best matching truck based on how many equipment types match its template.
    /// </summary>
    private Guid FindBestMatchingTruck(
        List<Equipment> scannedEquipment,
        List<TruckEquipmentTemplate> templates)
    {
        if (!templates.Any())
            return Guid.Empty;

        var typeCounts = scannedEquipment
            .GroupBy(e => e.EquipmentTypeId)
            .ToDictionary(g => g.Key, g => g.Count());

        var scores = templates
            .GroupBy(t => t.TruckId)
            .Select(g =>
            {
                var truckId = g.Key;
                int score = 0;
                foreach (var t in g)
                {
                    typeCounts.TryGetValue(t.EquipmentTypeId, out var scannedCount);
                    // score = min(scannedCount, requiredCount) so better matches rank higher
                    score += Math.Min(scannedCount, t.RequiredCount);
                }
                return new { TruckId = truckId, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var best = scores.FirstOrDefault();
        if (best == null || best.Score == 0)
            return Guid.Empty;

        return best.TruckId;
    }

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
                    IsResolved = false
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

internal class MissingEquipmentCases
{
    public Guid MissingEquipmentCaseId { get; set; }
    public Guid TruckId { get; set; }
    public Guid? DriverId { get; set; }
    public object SiteId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public object Status { get; set; }
    public object Severity { get; set; }
    public List<MissingEquipmentCaseItem> Items { get; set; }
}