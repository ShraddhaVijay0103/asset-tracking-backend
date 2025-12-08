namespace AssetTracking.Rfid.Domain.Entities;

public class Reader
{
    public Guid ReaderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Direction of vehicle flow at this reader: "Entry" (Check-In) or "Exit" (Check-Out)
    public string? Direction { get; set; }

    public ICollection<GateEvent> GateEvents { get; set; } = new List<GateEvent>();
    public ICollection<ReaderHeartbeat> Heartbeats { get; set; } = new List<ReaderHeartbeat>();
}
