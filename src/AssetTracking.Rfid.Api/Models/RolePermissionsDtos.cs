namespace AssetTracking.Rfid.Api.Models;

public class RolePermissions
{
    public string RoleName { get; set; } = string.Empty;
    public List<string> Modules { get; set; } = new();
}
