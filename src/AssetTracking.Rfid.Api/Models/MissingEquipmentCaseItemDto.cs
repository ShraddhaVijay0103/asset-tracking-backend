namespace AssetTracking.Rfid.Api.Models
{
    public class MissingEquipmentCaseItemDto
    {
        public Guid MissingEquipmentCaseItemId { get; set; }

        public Guid MissingEquipmentCaseId { get; set; }

        public Guid EquipmentId { get; set; }

        public string Epc { get; set; } = string.Empty;

        public bool IsRecovered { get; set; }

        // Always UTC
        public DateTimeOffset? RecoveredAt { get; set; }
    }

}
