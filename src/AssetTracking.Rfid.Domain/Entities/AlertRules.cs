namespace AssetTracking.Rfid.Domain.Entities;

public class AlertRules
{
    public Guid AlertRulesId { get; set; }
    public int MissingItemThreshold { get; set; } = 1;
    public int OverdueMinutes { get; set; } = 30;
    public bool NotifyEmail { get; set; } = true;
    public bool NotifySms { get; set; } = true;
    public bool NotifyPush { get; set; } = false;
}
