namespace AssetTracking.Rfid.Api.Models;

public class AlertRulesUpdateRequest
{
    public int MissingItemThreshold { get; set; }
    public int OverdueMinutes { get; set; }
    public bool NotifyEmail { get; set; }
    public bool NotifySms { get; set; }
    public bool NotifyPush { get; set; }
}
