namespace AssetTracking.Rfid.Api.Models;

public class RfidEvent
{
    public Guid ScanId { get; set; }
    public string Epc { get; set; } = string.Empty;
    public double Rssi { get; set; }
    public DateTime Timestamp { get; set; }
}

public class RfidEventBatch
{
    public string DeviceId { get; set; } = string.Empty;
    public string ReaderId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public List<RfidEvent> Events { get; set; } = new();
}
