namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class Company : MasterRecord
{
    public string? Logo { get; set; }
    public string? EmailId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public int? CountryId { get; set; }
    public int? StateId { get; set; }
    public CountryMaster? Country { get; set; }
    public StateMaster? State { get; set; }
}
