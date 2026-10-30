namespace Kodiak.ContainerTracking.DataAccess.Entities;
public sealed class ShippingPort : MasterRecord
{
    public int CountryId { get; set; }
    public int? StateId { get; set; }
    public string? City { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public CountryMaster Country { get; set; } = null!;
    public StateMaster? State { get; set; }
}
