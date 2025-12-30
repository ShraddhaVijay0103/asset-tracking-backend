namespace AssetTracking.Rfid.Api.Models
{
    public class TruckEquipmentAssignmentDto
    {
        public Guid AssignmentId { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? ReturnedAt { get; set; }

        public Guid TruckId { get; set; }
        public string? TruckNumber { get; set; }
        public string? TruckDescription { get; set; }
        public string? RfidTagId { get; set; }

        public Guid EquipmentId { get; set; }
        public string? EquipmentName { get; set; }
        public Guid? EquipmentTypeId { get; set; }

        public int? RequiredCount { get; set; }
    }

    public class TruckEquipmentDto
    {
        public Guid GetEventId { get; set; }
        public Guid TruckId { get; set; }
        public Guid EquipmentId { get; set; }
        public string? EquipmentName { get; set; }
        public string? EventType { get; set; }

    }
}
