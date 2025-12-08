namespace AssetTracking.Rfid.Api.Models;

public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
}

public class UpdateUserRoleRequest
{
    public Guid RoleId { get; set; }
}
