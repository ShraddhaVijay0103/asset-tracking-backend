namespace AssetTracking.Rfid.Domain.Entities;

public class RfidTag
{
    public Guid RfidTagId { get; set; }
    public string Epc { get; set; } = string.Empty;

    public ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}
