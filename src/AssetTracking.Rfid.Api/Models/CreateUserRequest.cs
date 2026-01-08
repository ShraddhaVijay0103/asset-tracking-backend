using System.ComponentModel.DataAnnotations;

namespace AssetTracking.Rfid.Api.Models;

using System.ComponentModel.DataAnnotations;

public class CreateUserRequest
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string LastName { get; set; } = string.Empty;

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
    [Compare(nameof(Password), ErrorMessage = "Password and ConfirmPassword do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public Guid RoleId { get; set; }
}

public class UpdateUserRoleRequest
{
    [Required]
    public Guid SiteId { get; set; }

    [Required]
    public Guid RoleId { get; set; }
}

public class UpdateUserRequest
{
    [StringLength(50, MinimumLength = 2)]
    public string? FirstName { get; set; }

    [StringLength(50, MinimumLength = 2)]
    public string? LastName { get; set; }

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

    [Compare(nameof(Password), ErrorMessage = "Password and ConfirmPassword do not match.")]
    public string? ConfirmPassword { get; set; }
}

public class CreateUserMultiSiteRequest
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string UserName { get; init; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(75)]
    public string Email { get; init; } = string.Empty;

    [StringLength(20)]
    [RegularExpression(@"^\+?[0-9]{7,20}$", ErrorMessage = "Invalid phone number.")]
    public string? PhoneNo { get; init; }

    [Required]
    [StringLength(50, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Password and ConfirmPassword do not match.")]
    public string ConfirmPassword { get; init; } = string.Empty;

    [Required]
    public List<Guid> Site { get; init; } = new List<Guid>();

    [Required]
    public List<Guid> Role { get; init; } = new List<Guid>();
}

public class UpdateUserMultiSiteRequest
{
    [StringLength(50, MinimumLength = 3)]
    public string? UserName { get; init; }

    [StringLength(50, MinimumLength = 2)]
    public string? FirstName { get; init; }

    [StringLength(50, MinimumLength = 2)]
    public string? LastName { get; init; }

    [EmailAddress]
    [StringLength(75)]
    public string? Email { get; init; }

    [StringLength(20)]
    [RegularExpression(@"^\+?[0-9]{7,20}$", ErrorMessage = "Invalid phone number.")]
    public string? PhoneNo { get; init; }

    [StringLength(50, MinimumLength = 8)]
    public string? Password { get; init; }

    [Compare(nameof(Password), ErrorMessage = "Password and ConfirmPassword do not match.")]
    public string? ConfirmPassword { get; init; }

    public List<Guid>? Site { get; init; } = new List<Guid>();
    public List<Guid>? Role { get; init; } = new List<Guid>();
}