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

    //// Runs every minute
    //[Function("ScanDataProcessor")] 
    //public async Task RunAsync(
    //    [TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo) // every 1 minute
    //{
    //    var now = DateTime.UtcNow;
    //    var lookbackMinutes = _config.GetValue<int>("ScanProcessor:LookbackMinutes", 30);
    //    var sessionWindowSeconds = _config.GetValue<int>("ScanProcessor:SessionWindowSeconds", 20);

    //    var since = now.AddMinutes(-lookbackMinutes);

    //    _logger.LogInformation("ScanDataProcessor started at {Time}, lookback: {Since}", now, since);

    //    // 1) Load unprocessed scans in lookback window
    //    var scans = await _db.RfidScans
    //        .Where(s => s.ProcessedAt == null && s.Timestamp >= since)
    //        .OrderBy(s => s.ReaderId)
    //        .ThenBy(s => s.Timestamp)
    //        .ToListAsync();

    //    if (!scans.Any())
    //    {
    //        _logger.LogInformation("No unprocessed scans found.");
    //        return;
    //    }

    //    _logger.LogInformation("Found {Count} unprocessed scans.", scans.Count);

    //    // 2) Group into sessions: ReaderId + time window
    //    var sessions = GroupIntoSessions(scans, TimeSpan.FromSeconds(sessionWindowSeconds));

    //    // Load templates & rules just once
    //    var templates = await _db.TruckEquipmentTemplates
    //        .Include(t => t.EquipmentType)
    //        .ToListAsync();

    //    var alertRules = await _db.AlertRules.FirstOrDefaultAsync()
    //        ?? new AlertRules
    //        {
    //            AlertRulesId = Guid.NewGuid(),
    //            MissingItemThreshold = 1,
    //            OverdueMinutes = 60,
    //            NotifyEmail = true,
    //            NotifySms = true,
    //            NotifyPush = false
    //        };

    //    if (alertRules.AlertRulesId == Guid.Empty)
    //    {
    //        alertRules.AlertRulesId = Guid.NewGuid();
    //        _db.AlertRules.Add(alertRules);
    //    }

    //    // Cache EPC -> Equipment
    //    var epcs = sessions.SelectMany(s => s.Scans).Select(s => s.Epc).Distinct().ToList();
    //    var equipmentByEpc = await _db.Equipment
    //        .Include(e => e.EquipmentType)
    //        .Include(e => e.RfidTag)
    //        .Where(e => e.RfidTag != null && epcs.Contains(e.RfidTag.Epc))
    //        .ToListAsync();

    //    // Process each session -> GateEvent + GateEventItems + Alerts
    //    foreach (var session in sessions)
    //    {
    //        await ProcessSessionAsync(session, equipmentByEpc, templates, alertRules, now);
    //    }

    //    // Mark scans as processed
    //    foreach (var scan in scans)
    //    {
    //        scan.ProcessedAt = now;
    //    }

    //    await _db.SaveChangesAsync();

    //    // Late return / not returned alerts (truck left long ago but no return)
    //    await ProcessLateReturnAlertsAsync(alertRules, now);

    //    _logger.LogInformation("ScanDataProcessor finished at {Time}", DateTime.UtcNow);
    //}

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
    private async Task ProcessSessionAsync(
        ScanSession session,
        List<Equipment> equipmentByEpc,
        List<TruckEquipmentTemplate> templates,
        AlertRules alertRules,
        DateTime now)
    {
        // Map EPCs -> Equipment
        var scannedEpcs = session.Scans.Select(s => s.Epc).Distinct().ToList();
        var scannedEquipment = equipmentByEpc
            .Where(e => e.RfidTag != null && scannedEpcs.Contains(e.RfidTag.Epc))
            .ToList();

        if (!scannedEquipment.Any())
        {
            // For now, ignore sessions with no known equipment
            _logger.LogInformation("Session at reader {ReaderId} has no known equipment EPCs.", session.ReaderId);
            return;
        }

        // 1) Determine truck by best matching template (Southern Botanical standard kits)
        var bestTruckId = FindBestMatchingTruck(scannedEquipment, templates);
        if (bestTruckId == Guid.Empty)
        {
            _logger.LogWarning("No matching truck found for session at reader {ReaderId}.", session.ReaderId);
            return;
        }

        // Load truck + driver
        var truck = await _db.Trucks
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.TruckId == bestTruckId);

        if (truck == null)
        {
            _logger.LogWarning("Truck {TruckId} not found in DB.", bestTruckId);
            return;
        }

        // 2) Determine event type (Entry vs Exit) from Reader.Direction
        // NOTE: RfidScan.ReaderId is string; we assume it stores Reader.ReaderId.ToString()
        var reader = await _db.Readers.FirstOrDefaultAsync(r => r.ReaderId.ToString() == session.ReaderId);
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
            var truckTemplates = templates.Where(t => t.TruckId == truck.TruckId).ToList();
            var expectedCount = truckTemplates.Sum(t => t.RequiredCount);
            var scannedCount = scannedEquipment.Count;

            if (expectedCount > scannedCount &&
                (expectedCount - scannedCount) >= alertRules.MissingItemThreshold)
            {
                var missingCount = expectedCount - scannedCount;

                var alert = new Alert
                {
                    AlertId = Guid.NewGuid(),
                    Timestamp = now,
                    Message =
                        $"{missingCount} equipment items missing for truck {truck.TruckNumber} during CHECK-OUT at reader {reader?.Name}. Truck driver: {truck.Driver?.FullName ?? "(unassigned)"}.",
                    Severity = "High",
                    Source = "GateEvent",
                    IsResolved = false
                };
                _db.Alerts.Add(alert);

                _logger.LogWarning("Exit event: truck {Truck}, driver {Driver}, missing {MissingCount} item(s).", truck.TruckNumber, truck.Driver?.FullName, missingCount);
            }
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

                    _logger.LogWarning("Entry event: truck {Truck}, driver {Driver}, missing {MissingCount} item(s): {MissingList}.",
                        truck.TruckNumber,
                        truck.Driver?.FullName,
                        missingEquipNames.Count,
                        missingList);
                }
            }
            else
            {
                _logger.LogInformation("Entry event for truck {TruckNumber}, but no previous Exit event found.", truck.TruckNumber);
            }
        }
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
