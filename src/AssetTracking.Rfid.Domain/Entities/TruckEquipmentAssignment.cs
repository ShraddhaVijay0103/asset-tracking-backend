using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("truck_equipment_assignments")]
public class TruckEquipmentAssignment
{
    [Column("assignment_id")]
    public Guid AssignmentId { get; set; }

    [Column("truck_id")]
    public Guid TruckId { get; set; }

    public Truck? Truck { get; set; }

    [Column("equipment_id")]
    public Guid EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    [Column("assigned_at")]
    public DateTime AssignedAt { get; set; }

    [Column("returned_at")]
    public DateTime? ReturnedAt { get; set; }
}
