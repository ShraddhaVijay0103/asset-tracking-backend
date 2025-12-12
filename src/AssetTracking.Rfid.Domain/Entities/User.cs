using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("users")]
public class User
{
     public Guid UserId { get; set; }

        [Column("fullname")]  // <-- change based on your DB
        public string FullName { get; set; } = string.Empty;

        [Column("username")]  // check if needed
        public string UserName { get; set; } = string.Empty;

        [Column("phoneno")]   // check if needed
        public string PhoneNo { get; set; } = string.Empty;

        [Column("password")]  // check if needed
        public string Password { get; set; } = string.Empty;

        [Column("confirmpassword")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Column("email")]     // check if needed
        public string Email { get; set; } = string.Empty;
    [Column("siteid")]
    public Guid SiteId { get; set; }
        public Site? Site { get; set; }

        public Guid RoleId { get; set; }
        public Role? Role { get; set; }
    }
