namespace AssetTracking.Rfid.Api.Models
{
    public class MissingEquipmentCaseDtos
    {
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
