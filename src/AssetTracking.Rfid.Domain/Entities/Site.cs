using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("site")]
public class Site
{
    public Guid SiteId { get; set; }
    public string Name { get; set; } = string.Empty;
}