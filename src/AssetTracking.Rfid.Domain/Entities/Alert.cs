using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("alerts")]
public class Alert
{
    [Key]
    [Column("alert_id")]
    public Guid AlertId { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Required]
    [Column("severity")]
    public string Severity { get; set; } = "Medium"; // "High", "Medium", "Low"

    [Required]
    [Column("source")]
    public string Source { get; set; } = string.Empty; // e.g., "GateEvent", "Reader", "System"

    [Column("is_resolved")]
    public bool IsResolved { get; set; } = false;

    [Column("resolved_by_user")]
    public Guid? ResolvedByUser { get; set; }
    public User? ResolvedByUserNavigation { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [Column("site_id")]
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
}
