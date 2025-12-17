using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("reader_heartbeats")]
public class ReaderHeartbeat
{
    [Column("heartbeat_id")]
    public Guid HeartbeatId { get; set; }
    [Column("reader_id")]
    public Guid ReaderId { get; set; }    
    public Reader? Reader { get; set; }
    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
