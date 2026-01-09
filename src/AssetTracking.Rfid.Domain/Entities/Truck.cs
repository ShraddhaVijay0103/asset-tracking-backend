using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("trucks")]
public class Truck
{
    [Key]
    [Column("truck_id")]
    public Guid TruckId { get; set; }

    [Required]
    [Column("truck_number")]
    public string TruckNumber { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("driver_id")]
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    [Column("site_id")]
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }

    [Column("rfid_tag_id")]
    public Guid RfidTagId { get; set; }
    public RfidTag? RfidTag { get; set; }

    // Optional: Soft delete / audit columns
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GateEvent> GateEvents { get; set; } = new List<GateEvent>();
    public ICollection<TruckEquipmentTemplate> EquipmentTemplates { get; set; } = new List<TruckEquipmentTemplate>();
}

