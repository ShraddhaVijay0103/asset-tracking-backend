using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Rfid.Api.Models
{
    public class MissingEquipmentCaseDto
    {
        public Guid MissingEquipmentCaseId { get; set; }

        public Guid TruckId { get; set; }

        public Guid? DriverId { get; set; }

        public Guid? SiteId { get; set; }

        public int StatusId { get; set; }

        public int SeverityId { get; set; }

        // Always UTC
        public DateTimeOffset OpenedAt { get; set; }

        public DateTimeOffset? LastSeenAt { get; set; }

        public DateTimeOffset? ClosedAt { get; set; }

        public string? Notes { get; set; }

        // Navigation-style data for API responses
        public List<MissingEquipmentCaseItemDto> Items { get; set; } = new();
    }

    public class MissingEquipmentCountsDto
    {
        public int TodayMissing { get; set; }
        public int ThisWeekMissing { get; set; }
        public int ThisMonthMissing { get; set; }
        public int LongOverdue { get; set; }
        public int RecentlyClearedToday { get; set; }
    }



}
