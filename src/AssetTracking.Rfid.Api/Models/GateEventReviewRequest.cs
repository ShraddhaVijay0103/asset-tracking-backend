namespace AssetTracking.Rfid.Api.Models;

public class GateEventReviewRequest
{
    public string Status { get; set; } = string.Empty; // Approved / Rejected
    public string? Notes { get; set; }
}
