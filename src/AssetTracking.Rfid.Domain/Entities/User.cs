using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("users")]
public class User
{
     public Guid UserId { get; set; }

        [Column("full_name")]  // <-- change based on your DB
        public string FullName { get; set; } = string.Empty;

        [Column("user_name")]  // check if needed
        public string UserName { get; set; } = string.Empty;

        [Column("phone_no")]   // check if needed
        public string PhoneNo { get; set; } = string.Empty;

        [Column("password")]  // check if needed
        public string Password { get; set; } = string.Empty;

        [Column("confirm_password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Column("email")]     // check if needed
        public string Email { get; set; } = string.Empty;
    
        [Column("site_id")]
        public Guid SiteId { get; set; }
        
        public Site? Site { get; set; }

        [Column("role_id")]
        public Guid RoleId { get; set; }

        public Role? Role { get; set; }
    }
