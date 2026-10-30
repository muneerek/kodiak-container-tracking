namespace Kodiak.ContainerTracking.DataAccess.Entities;

public abstract class AuditableEntity
{
    public DateTimeOffset CreateOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset UpdatedOn { get; set; }
    public string? UpdatedBy { get; set; }
}
