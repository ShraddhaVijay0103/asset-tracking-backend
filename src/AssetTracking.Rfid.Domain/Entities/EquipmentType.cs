namespace AssetTracking.Rfid.Domain.Entities;

public class EquipmentType
{
    public Guid EquipmentTypeId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}
