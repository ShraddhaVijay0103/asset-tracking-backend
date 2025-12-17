using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;
[Table("alert_rules")]
public class AlertRules
{

    [Column("alert_rules_id")]
    public Guid AlertRulesId { get; set; }
    [Column("missing_item_threshold")]
    public int MissingItemThreshold { get; set; } = 1;
    [Column("overdue_minutes")]
    public int OverdueMinutes { get; set; } = 30;
    [Column("notify_email")]
    public bool NotifyEmail { get; set; } = true;
    [Column("notify_sms")]
    public bool NotifySms { get; set; } = true;
    [Column("notify_push")]
    public bool NotifyPush { get; set; } = false;
}
