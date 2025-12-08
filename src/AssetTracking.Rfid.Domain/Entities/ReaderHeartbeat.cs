namespace AssetTracking.Rfid.Domain.Entities;

public class ReaderHeartbeat
{
    public Guid HeartbeatId { get; set; }
    public Guid ReaderId { get; set; }
    public Reader? Reader { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
