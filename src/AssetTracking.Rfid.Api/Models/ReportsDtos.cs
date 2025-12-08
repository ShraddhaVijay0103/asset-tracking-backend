namespace AssetTracking.Rfid.Api.Models;

public class MissingEquipmentReportRow
{
    public DateTime EventTime { get; set; }
    public string TruckNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string ReaderName { get; set; } = string.Empty;
    public int ExpectedCount { get; set; }
    public int ScannedCount { get; set; }
    public int MissingCount { get; set; }
}

public class TruckHistoryRow
{
    public DateTime EventTime { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ReaderName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class ReaderHealthRow
{
    public string ReaderName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime? LastHeartbeat { get; set; }
    public bool IsOnline { get; set; }
}
