namespace AssetTracking.Rfid.Api.Models
{
    public class TruckListResponse
    {
        public Guid TruckId { get; set; }
        public string TruckNumber { get; set; } = string.Empty;
        public string SiteName { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
    }
}
