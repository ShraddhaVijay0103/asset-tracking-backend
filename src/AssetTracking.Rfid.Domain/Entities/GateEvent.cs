namespace AssetTracking.Rfid.Domain.Entities;

public class GateEvent
{
    public Guid GateEventId { get; set; }
    public DateTime EventTime { get; set; }
    public string EventType { get; set; } = "Exit"; // Entry / Exit

    public Guid ReaderId { get; set; }
    public Reader? Reader { get; set; }

    public Guid TruckId { get; set; }
    public Truck? Truck { get; set; }

    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public string Status { get; set; } = "Pending"; // Pending / Approved / Rejected
    public string? Notes { get; set; }

    public ICollection<GateEventItem> Items { get; set; } = new List<GateEventItem>();
}
