using System.ComponentModel.DataAnnotations;

namespace AssetTracking.Rfid.Api.Models;

using System.ComponentModel.DataAnnotations;

public class CreateUserRequest
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string UserName { get; set; } = string.Empty;

    [StringLength(10)]
    [RegularExpression(@"^\+?[0-9]{7,20}$", ErrorMessage = "Invalid phone number.")]
    public string? PhoneNo { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(75)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 8)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public Guid RoleId { get; set; }

    // SiteId will come from route
}


public class UpdateUserRoleRequest
{
    public Guid RoleId { get; set; }
}

public class UpdateUserRequest
{
    [StringLength(50, MinimumLength = 2)]
    public string? FullName { get; set; }

    [StringLength(50, MinimumLength = 3)]
    public string? UserName { get; set; }

    [StringLength(10)]
    [RegularExpression(@"^\+?[0-9]{7,20}$", ErrorMessage = "Invalid phone number.")]
    public string? PhoneNo { get; set; }

    [EmailAddress]
    [StringLength(75)]
    public string? Email { get; set; }

    [StringLength(50, MinimumLength = 8)]
    public string? Password { get; set; }

    [StringLength(50, MinimumLength = 8)]
    public string? ConfirmPassword { get; set; }

    public Guid? SiteId { get; set; }

    public Guid? RoleId { get; set; }
}