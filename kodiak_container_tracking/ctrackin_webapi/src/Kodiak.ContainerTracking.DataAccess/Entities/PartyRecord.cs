namespace Kodiak.ContainerTracking.DataAccess.Entities;

// Shared CLR fields; each concrete master maps to its own complete table.
public abstract class PartyRecord : AuditableEntity
{
    public int UniqueId { get; set; }
    public PartyType PartyType { get; set; }
    public string UniqueCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EmailId { get; set; }
    public string? Address { get; set; }
    public string? Logo { get; set; }
    public long Version { get; set; } = 1;
    public PartyReference Reference { get; set; } = null!;
}
