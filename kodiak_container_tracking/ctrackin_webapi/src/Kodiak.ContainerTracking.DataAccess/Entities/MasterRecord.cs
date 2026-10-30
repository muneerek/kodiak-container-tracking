namespace Kodiak.ContainerTracking.DataAccess.Entities;

public abstract class MasterRecord : AuditableEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
}
