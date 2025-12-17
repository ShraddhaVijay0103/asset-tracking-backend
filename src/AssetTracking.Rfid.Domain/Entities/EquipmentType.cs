using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("equipment_types", Schema = "public")]
public class EquipmentType
{
    [Column("equipment_type_id")]
    public Guid EquipmentTypeId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    public ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}

