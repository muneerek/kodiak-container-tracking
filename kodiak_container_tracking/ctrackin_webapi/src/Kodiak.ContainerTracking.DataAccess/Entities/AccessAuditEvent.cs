namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class AccessAuditEvent
{
    public long EventId { get; set; }
    public DateTimeOffset OccurredOn { get; set; }
    public string ActorSubject { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Changes { get; set; } = string.Empty;
}
