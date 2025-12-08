namespace AssetTracking.Rfid.Domain.Entities;

public class Alert
{
    public Guid AlertId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium"; // High / Medium / Low
    public string Source { get; set; } = string.Empty; // GateEvent / Reader / System
    public bool IsResolved { get; set; }
    public Guid? ResolvedByUser { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
