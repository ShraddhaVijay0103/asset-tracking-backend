namespace AssetTracking.Rfid.Domain.Entities;

public class Truck
{
    public Guid TruckId { get; set; }
    public string TruckNumber { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public ICollection<GateEvent> GateEvents { get; set; } = new List<GateEvent>();
    public ICollection<TruckEquipmentTemplate> EquipmentTemplates { get; set; } = new List<TruckEquipmentTemplate>();
}
