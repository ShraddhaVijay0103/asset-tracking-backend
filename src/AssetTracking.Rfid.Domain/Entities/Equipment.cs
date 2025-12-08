namespace AssetTracking.Rfid.Domain.Entities;

public class Equipment
{
    public Guid EquipmentId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Guid EquipmentTypeId { get; set; }
    public EquipmentType? EquipmentType { get; set; }

    public Guid RfidTagId { get; set; }
    public RfidTag? RfidTag { get; set; }

    public ICollection<GateEventItem> GateEventItems { get; set; } = new List<GateEventItem>();
}
