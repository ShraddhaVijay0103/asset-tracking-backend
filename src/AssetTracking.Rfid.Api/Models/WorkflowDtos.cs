namespace AssetTracking.Rfid.Api.Models;

public class WorkflowScanItem
{
    public string Epc { get; set; } = string.Empty;
    public int Rssi { get; set; }
}

public class CheckoutRequest
{
    public Guid TruckId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? ReaderId { get; set; }
    public string SiteId { get; set; } = string.Empty;
    public List<WorkflowScanItem> Items { get; set; } = new();
}

public class CheckinRequest
{
    public Guid TruckId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? ReaderId { get; set; }
    public string SiteId { get; set; } = string.Empty;
    public List<WorkflowScanItem> Items { get; set; } = new();
    public string? Notes { get; set; }
}

public class WorkflowItemResult
{
    public string Epc { get; set; } = string.Empty;
    public string? EquipmentName { get; set; }
    public string? EquipmentType { get; set; }
    public string Status { get; set; } = string.Empty; // Expected / Extra / Unknown / Missing
}

public class WorkflowResult
{
    public Guid GateEventId { get; set; }
    public string EventType { get; set; } = string.Empty; // Exit or Entry
    public int ExpectedCount { get; set; }
    public int ScannedCount { get; set; }
    public int MissingCount { get; set; }
    public int ExtraCount { get; set; }
    public List<WorkflowItemResult> Items { get; set; } = new();
}
