using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("missing_equipment_case_items")]
public class MissingEquipmentCaseItem
{
    [Key]
    [Column("missing_equipment_case_item_id")]
    public Guid MissingEquipmentCaseItemId { get; set; }

    [Column("missing_equipment_case_id")]
    public Guid MissingEquipmentCaseId { get; set; }
    public MissingEquipmentCase? MissingEquipmentCase { get; set; }

    [Column("equipment_id")]
    public Guid EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    [Column("epc")]
    public string Epc { get; set; } = string.Empty;

    [Column("is_recovered")]
    public bool IsRecovered { get; set; }

    [Column("recovered_at")]
    public DateTimeOffset? RecoveredAt { get; set; }
}
