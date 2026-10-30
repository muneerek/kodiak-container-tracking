namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class UniqueCodeCounter
{
    public PartyType PartyType { get; set; }
    public long LastNumber { get; set; }
}
