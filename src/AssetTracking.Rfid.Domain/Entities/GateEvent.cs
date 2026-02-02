using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("gate_events")]
public class GateEvent
{
    [Column("gate_event_id")]
    public Guid GateEventId { get; set; }

    [Column("event_time")]
    public DateTime EventTime { get; set; }

    [Column("event_type")]
    public string EventType { get; set; } = "Exit"; // Entry / Exit

    [Column("reader_id")]
    public Guid ReaderId { get; set; }

    public Reader? Reader { get; set; }

    [Column("truck_id")]
    public Guid TruckId { get; set; }

    public Truck? Truck { get; set; }

    [Column("driver_id")]
    public Guid? DriverId { get; set; }

    public Driver? Driver { get; set; }

      [Column("status")]
    public string Status { get; set; } = "Pending"; // Pending / Approved / Rejected

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("site_id")]
    public Guid SiteId { get; set; }

  public ICollection<GateEventItem> Items { get; set; } = new List<GateEventItem>();
}
