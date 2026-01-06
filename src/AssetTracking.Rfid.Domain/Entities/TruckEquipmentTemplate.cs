using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("truck_equipment_templates")]

public class TruckEquipmentTemplate
{
    [Column("template_id")]
    public Guid TemplateId { get; set; }
    [Column("truck_id")]
    public Guid TruckId { get; set; }
    public Truck? Truck { get; set; }
    [Column("equipment_type_id")]
    public Guid EquipmentTypeId { get; set; }
    public EquipmentType? EquipmentType { get; set; }
    [Column("required_count")]
    public int RequiredCount { get; set; }
    [Column("site_id")]
    public Guid SiteId { get; set; }
}

