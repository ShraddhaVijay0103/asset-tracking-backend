using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("audit_logs")]
public class AuditLog
{
    [Column("audit_log_id")]
    public Guid AuditLogId { get; set; }
    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    [Column("user_name")]
    public string UserName { get; set; } = string.Empty;
    [Column("action")]
    public string Action { get; set; } = string.Empty;
    [Column("module")]
    public string Module { get; set; } = string.Empty;
    [Column("ip_address")]
    public string IpAddress { get; set; } = string.Empty;
}
