using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("rfid_scans")]
public class RfidScan
{
    [Column("scan_id")]
    public Guid ScanId { get; set; }

    [Column("epc")]
    public string Epc { get; set; } = string.Empty;

    [Column("rssi")]
    public double Rssi { get; set; }

    [Column("reader_id")]
    public string ReaderId { get; set; } = string.Empty;

    [Column("site_id")]
    public string SiteId { get; set; } = string.Empty;

    [Column("timestamp")]
    public DateTime Timestamp { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }
}
