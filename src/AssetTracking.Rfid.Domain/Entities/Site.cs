using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using AssetTracking.Rfid.Api.Models;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("sites")] 
public class Site
{
    [Key]
    [Column("site_id")]
    public Guid SiteId { get; set; }

    [Required]
    [Column("site_name")]
    public string Name { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserSiteRole> UserSiteRoles { get; set; } = new List<UserSiteRole>();
}
