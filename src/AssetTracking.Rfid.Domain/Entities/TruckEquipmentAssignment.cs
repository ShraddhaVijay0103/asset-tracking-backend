using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("truck_equipment_assignments")]
public class TruckEquipmentAssignment
{
    [Key]
    [Column("assignment_id")]
    public Guid AssignmentId { get; set; }

    [Column("truck_id")]
    public Guid TruckId { get; set; }
    public Truck? Truck { get; set; }

    [Column("equipment_id")]
    public Guid EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    [Column("assigned_at")]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [Column("returned_at")]
    public DateTime? ReturnedAt { get; set; }

    [Column("site_id")]
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }

    // Optional soft‑delete / audit
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
