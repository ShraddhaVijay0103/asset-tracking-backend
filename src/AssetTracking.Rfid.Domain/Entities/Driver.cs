using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("drivers")]
public class Driver
{
    [Column("driver_id")]
    public Guid DriverId { get; set; }
    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;
    [Column("phone")]
    public string? Phone { get; set; }

    public ICollection<Truck> Trucks { get; set; } = new List<Truck>();
}
