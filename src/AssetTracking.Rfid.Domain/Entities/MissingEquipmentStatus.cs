using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("missing_equipment_status")]
public class MissingEquipmentStatus
{
    [Key]
    [Column("status_id")]
    public int StatusId { get; set; }

    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("is_final")]
    public bool IsFinal { get; set; }

    public ICollection<MissingEquipmentCase> Cases { get; set; }
        = new List<MissingEquipmentCase>();
}

