using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("devices")]
public class Device
{
    [Column("device_id")]
    public Guid DeviceId { get; set; }
    [Column("type")]
    public string Type { get; set; } = string.Empty; // reader / edge_gateway
    [Column("location")]
    public string Location { get; set; } = string.Empty;
    [Column("status")]
    public string Status { get; set; } = "Online";
    [Column("last_seen")]
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}
