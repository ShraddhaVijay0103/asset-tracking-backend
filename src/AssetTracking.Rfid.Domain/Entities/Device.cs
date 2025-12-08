namespace AssetTracking.Rfid.Domain.Entities;

public class Device
{
    public Guid DeviceId { get; set; }
    public string Type { get; set; } = string.Empty; // reader / edge_gateway
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = "Online";
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}
