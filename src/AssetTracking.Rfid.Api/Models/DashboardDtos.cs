namespace AssetTracking.Rfid.Api.Models;

public class DashboardSummary
{
    public int TrucksInside { get; set; }
    public int TrucksEnRoute { get; set; }
    public int GateEventsLastHour { get; set; }
    public int TotalScansToday { get; set; }

    public int ActiveAlerts { get; set; }
    public int CriticalAlerts { get; set; }

    public double EquipmentLossRatePercent { get; set; }
    public double ReaderUptimePercent { get; set; }

    public int ItemsLeavingToday { get; set; }
    public int ItemsReturningToday { get; set; }
    public int ExceptionsToday { get; set; }
}
