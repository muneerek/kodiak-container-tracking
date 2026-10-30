namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class Consignee : PartyRecord
{
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public BusinessStatus Status { get; set; } = BusinessStatus.Active;
    public int? CountryId { get; set; }
    public CountryMaster? Country { get; set; }
}
