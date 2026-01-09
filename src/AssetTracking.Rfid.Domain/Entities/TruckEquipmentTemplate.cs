using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("truck_equipment_templates")]
public class TruckEquipmentTemplate
{
    [Key]
    [Column("template_id")]
    public Guid TemplateId { get; set; }

    [Required]
    [Column("truck_id")]
    public Guid TruckId { get; set; }
    public Truck? Truck { get; set; }

    [Required]
    [Column("equipment_type_id")]
    public Guid EquipmentTypeId { get; set; }
    public EquipmentType? EquipmentType { get; set; }

    [Required]
    [Column("required_count")]
    public int RequiredCount { get; set; }

    [Required]
    [Column("site_id")]
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }

    // Optional soft‑delete / audit fields
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

