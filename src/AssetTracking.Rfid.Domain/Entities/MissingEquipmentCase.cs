using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssetTracking.Rfid.Domain.Entities;

[Table("missing_equipment_cases")]
public class MissingEquipmentCase
{
    [Key]
    [Column("missing_equipment_case_id")]
    public Guid MissingEquipmentCaseId { get; set; }

    [Column("truck_id")]
    public Guid TruckId { get; set; }
    public Truck? Truck { get; set; }

    [Column("driver_id")]
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    [Column("site_id")]
    public Guid? SiteId { get; set; }
    public Site? Site { get; set; }

    [Column("status_id")]
    public int StatusId { get; set; }
    public MissingEquipmentStatus? Status { get; set; }

    [Column("severity_id")]
    public int SeverityId { get; set; }
    public MissingEquipmentSeverity? Severity { get; set; }

    [Column("opened_at")]
    public DateTimeOffset OpenedAt { get; set; }

    [Column("last_seen_at")]
    public DateTimeOffset? LastSeenAt { get; set; }

    [Column("closed_at")]
    public DateTimeOffset? ClosedAt { get; set; }
     
    [Column("open_notes")]
    public string? OpenNotes { get; set; }

    [Column("investigation_notes")]
    public string? InvestigationNotes { get; set; }

    [Column("recovered_notes")]
    public string? RecoveredNotes { get; set; }

    [Column("closed_notes")]
    public string? ClosedNotes { get; set; }

    public ICollection<MissingEquipmentCaseItem> Items { get; set; }
        = new List<MissingEquipmentCaseItem>();
}


