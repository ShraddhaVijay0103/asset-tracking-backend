using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("missing_equipment_severity")]
public class MissingEquipmentSeverity
{

    [Key]
    [Column("severity_id")]
    public int SeverityId { get; set; }

    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("priority")]
    public int Priority { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("cost")]
    public string? Cost { get; set; }

    public ICollection<MissingEquipmentCase> Cases { get; set; }
        = new List<MissingEquipmentCase>();
}

