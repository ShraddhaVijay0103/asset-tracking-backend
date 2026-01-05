using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using AssetTracking.Rfid.Domain.Entities;

namespace AssetTracking.Rfid.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();
    public DbSet<RfidTag> RfidTags => Set<RfidTag>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Truck> Trucks => Set<Truck>();
    public DbSet<TruckEquipmentTemplate> TruckEquipmentTemplates => Set<TruckEquipmentTemplate>();
    public DbSet<TruckEquipmentAssignment> TruckEquipmentAssignments => Set<TruckEquipmentAssignment>();
    public DbSet<Reader> Readers => Set<Reader>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<GateEvent> GateEvents => Set<GateEvent>();
    public DbSet<GateEventItem> GateEventItems => Set<GateEventItem>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertRules> AlertRules => Set<AlertRules>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReaderHeartbeat> ReaderHeartbeats => Set<ReaderHeartbeat>();
    public DbSet<RfidScan> RfidScans => Set<RfidScan>();
    

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //modelBuilder.UseSnakeCaseNamingConvention();

        modelBuilder.Entity<EquipmentType>().HasKey(e => e.EquipmentTypeId);
        modelBuilder.Entity<RfidTag>().HasKey(e => e.RfidTagId);
        modelBuilder.Entity<Equipment>().HasKey(e => e.EquipmentId);
        modelBuilder.Entity<Driver>().HasKey(e => e.DriverId);
        modelBuilder.Entity<Truck>().HasKey(e => e.TruckId);
        modelBuilder.Entity<TruckEquipmentTemplate>().HasKey(t => t.TemplateId);
        modelBuilder.Entity<TruckEquipmentAssignment>().HasKey(t => t.AssignmentId);
        modelBuilder.Entity<Reader>().HasKey(e => e.ReaderId);
        modelBuilder.Entity<Device>().HasKey(e => e.DeviceId);
        modelBuilder.Entity<GateEvent>().HasKey(e => e.GateEventId);
        modelBuilder.Entity<GateEventItem>().HasKey(e => e.GateEventItemId);
        modelBuilder.Entity<Alert>().HasKey(e => e.AlertId);
        modelBuilder.Entity<AlertRules>().HasKey(e => e.AlertRulesId);
        modelBuilder.Entity<Role>().HasKey(e => e.RoleId);
        modelBuilder.Entity<User>().HasKey(e => e.UserId);
        modelBuilder.Entity<AuditLog>().HasKey(e => e.AuditLogId);
        modelBuilder.Entity<ReaderHeartbeat>().HasKey(e => e.HeartbeatId);
        modelBuilder.Entity<RfidScan>().HasKey(e => e.ScanId);
        modelBuilder.Entity<MissingEquipmentCase>().HasKey(e => e.MissingEquipmentCaseId);
        modelBuilder.Entity<MissingEquipmentCaseItem>().HasKey(e => e.MissingEquipmentCaseItemId);
        modelBuilder.Entity<MissingEquipmentSeverity>().HasKey(e => e.SeverityId);
        modelBuilder.Entity<MissingEquipmentStatus>().HasKey(e => e.StatusId);

    }
}
