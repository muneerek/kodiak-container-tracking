namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class StateMaster : MasterRecord
{
    public int CountryId { get; set; }
    public CountryMaster Country { get; set; } = null!;
}
