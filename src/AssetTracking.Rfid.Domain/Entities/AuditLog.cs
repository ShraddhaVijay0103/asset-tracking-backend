namespace AssetTracking.Rfid.Domain.Entities;

public class AuditLog
{
    public Guid AuditLogId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
