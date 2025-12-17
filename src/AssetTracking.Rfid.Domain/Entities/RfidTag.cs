using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("rfid_tags", Schema = "public")]
public class RfidTag
{
    [Column("rfid_tag_id")]
    public Guid RfidTagId { get; set; }
    [Column("epc")]
    public string Epc { get; set; } = string.Empty;

    public ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}
