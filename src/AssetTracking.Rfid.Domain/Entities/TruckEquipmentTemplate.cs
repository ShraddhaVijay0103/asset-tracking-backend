namespace AssetTracking.Rfid.Domain.Entities;

public class TruckEquipmentTemplate
{
    public Guid TemplateId { get; set; }

    public Guid TruckId { get; set; }
    public Truck? Truck { get; set; }

    public Guid EquipmentTypeId { get; set; }
    public EquipmentType? EquipmentType { get; set; }

    public int RequiredCount { get; set; }
}
