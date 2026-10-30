namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class Shipper : PartyRecord
{
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public int? ConsigneeId { get; set; }
    public Consignee? Consignee { get; set; }
    public BusinessStatus Status { get; set; } = BusinessStatus.Active;
    public int? CountryId { get; set; }
    public int? BusinessCategoryId { get; set; }
    public CountryMaster? Country { get; set; }
    public BusinessCategory? BusinessCategory { get; set; }
}
