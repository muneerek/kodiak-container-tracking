namespace Kodiak.ContainerTracking.DataAccess.Entities;

// Shared identity registry makes the polymorphic user reference a real database FK.
public sealed class PartyReference
{
    public int UniqueId { get; set; }
    public PartyType PartyType { get; set; }
    public string UniqueCode { get; set; } = string.Empty;
    public Employee? Employee { get; set; }
    public Consignee? Consignee { get; set; }
    public Shipper? Shipper { get; set; }
}
