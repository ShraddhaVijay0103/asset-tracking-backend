using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("drivers")]
public class Driver
{
    [Key]
    [Column("driver_id")]
    public Guid DriverId { get; set; }

    [Required]
    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Truck> Trucks { get; set; } = new List<Truck>();
}

