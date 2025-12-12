namespace AssetTracking.Rfid.Api.Models;

public class CreateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string PhoneNo { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid SiteId { get; set; }
    public Guid RoleId { get; set; }
}

public class UpdateUserRoleRequest
{
    public Guid RoleId { get; set; }
}
