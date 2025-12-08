namespace AssetTracking.Rfid.Domain.Entities;

public class GateEventItem
{
    public Guid GateEventItemId { get; set; }

    public Guid GateEventId { get; set; }
    public GateEvent? GateEvent { get; set; }

    public Guid EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public string Epc { get; set; } = string.Empty;
}
