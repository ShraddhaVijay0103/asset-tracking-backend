using AssetTracking.Rfid.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Api.Models;

[Table("user_site_roles")]
public class UserSiteRole
{
    [Key]
    [Column("user_site_role_id")]
    public Guid UserSiteRoleId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }
    public User? User { get; set; }

    [Column("site_id")]
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }

    [Column("role_id")]
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("assigned_at")]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

