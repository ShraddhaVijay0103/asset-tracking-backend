namespace AssetTracking.Rfid.Domain.Entities;

public class User
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}
