using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("alerts")]
public class Alert
{
    [Column("alert_id")]
    public Guid AlertId { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("severity")]
    public string Severity { get; set; } = "Medium"; // High / Medium / Low

    [Column("source")]
    public string Source { get; set; } = string.Empty; // GateEvent / Reader / System

    [Column("is_resolved")]
    public bool IsResolved { get; set; }

    [Column("resolved_by_user")]
    public Guid? ResolvedByUser { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }
}
