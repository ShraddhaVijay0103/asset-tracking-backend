using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("gate_event_items")]
public class GateEventItem
{
    [Column("gate_event_item_id")]
    public Guid GateEventItemId { get; set; }

    [Column("gate_event_id")]
    public Guid GateEventId { get; set; }

    public GateEvent? GateEvent { get; set; }

    [Column("equipment_id")]
    public Guid EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    [Column("epc")]
    public string Epc { get; set; } = string.Empty;
}
