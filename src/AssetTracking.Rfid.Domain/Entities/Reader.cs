using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("readers")]
public class Reader
{
    [Column("reader_id")]
    public Guid ReaderId { get; set; }
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    [Column("location")]
    public string Location { get; set; } = string.Empty;
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    // Direction of vehicle flow at this reader: "Entry" (Check-In) or "Exit" (Check-Out)
    [Column("direction")]
    public string? Direction { get; set; }
    [Column("site_id")]
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public ICollection<GateEvent> GateEvents { get; set; } = new List<GateEvent>();
    public ICollection<ReaderHeartbeat> Heartbeats { get; set; } = new List<ReaderHeartbeat>();
}
