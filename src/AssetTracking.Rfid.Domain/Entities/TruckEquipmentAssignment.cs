namespace AssetTracking.Rfid.Domain.Entities;

public class TruckEquipmentAssignment
{
    public Guid AssignmentId { get; set; }

    public Guid TruckId { get; set; }
    public Truck? Truck { get; set; }

    public Guid EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public DateTime AssignedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
}
