using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("trucks")]
public class Truck
{
    [Column("truck_id")]
    public Guid TruckId { get; set; }

    [Column("truck_number")]
    public string TruckNumber { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("driver_id")]
    public Guid? DriverId { get; set; }

    public Driver? Driver { get; set; }

    [Column("siteid")]
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public ICollection<GateEvent> GateEvents { get; set; } = new List<GateEvent>();
    public ICollection<TruckEquipmentTemplate> EquipmentTemplates { get; set; } = new List<TruckEquipmentTemplate>();
}
