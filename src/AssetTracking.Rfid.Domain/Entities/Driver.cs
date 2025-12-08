namespace AssetTracking.Rfid.Domain.Entities;

public class Driver
{
    public Guid DriverId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public ICollection<Truck> Trucks { get; set; } = new List<Truck>();
}
