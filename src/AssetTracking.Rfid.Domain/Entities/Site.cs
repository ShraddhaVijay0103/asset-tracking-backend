using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("site")]
public class Site
{
    [Column("site_id")]
    public Guid SiteId { get; set; }
    [Column("name")]
    public string Name { get; set; } = string.Empty;
}