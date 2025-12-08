namespace AssetTracking.Rfid.Domain.Entities;

public class RfidScan
{
    public Guid ScanId { get; set; }
    public string Epc { get; set; } = string.Empty;
    public int Rssi { get; set; }
    public string ReaderId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    // Mark when this scan has been processed by the Scan Data Processor
    public DateTime? ProcessedAt { get; set; }
}
