using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("equipment", Schema = "public")]
public class Equipment
{
    [Column("equipment_id")]
    public Guid EquipmentId { get; set; }
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    [Column("equipment_type_id")]
    public Guid EquipmentTypeId { get; set; }
    public EquipmentType? EquipmentType { get; set; }
    [Column("rfid_tag_id")]
    public Guid RfidTagId { get; set; }
    public RfidTag? RfidTag { get; set; }

    public ICollection<GateEventItem> GateEventItems { get; set; } = new List<GateEventItem>();
}
