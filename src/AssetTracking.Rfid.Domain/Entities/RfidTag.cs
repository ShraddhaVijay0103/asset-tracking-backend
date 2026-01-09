using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("rfid_tags", Schema = "public")]
public class RfidTag
{
    [Column("rfid_tag_id")]
    public Guid RfidTagId { get; set; }

    [Column("tag_name")]
    public string TagName { get; set; }= string.Empty;

    [Column("site_id")]
    public Guid SiteId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}
