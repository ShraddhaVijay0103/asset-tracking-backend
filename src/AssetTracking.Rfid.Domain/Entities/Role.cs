using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("roles")]
public class Role
{
    [Column("role_id")]
    public Guid RoleId { get; set; }
    [Column("name")]
    public string Name { get; set; } = string.Empty;
}
