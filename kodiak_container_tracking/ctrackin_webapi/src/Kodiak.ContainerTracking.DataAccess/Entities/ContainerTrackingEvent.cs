namespace Kodiak.ContainerTracking.DataAccess.Entities;
public enum ContainerTrackingStatus { Booked = 1, Loaded = 2, InTransit = 3, Arrived = 4, CustomsClearance = 5, Delivered = 6 }
public sealed class ContainerTrackingEvent : AuditableEntity
{
    public int Id { get; set; }
    public int ContainerRegistrationId { get; set; }
    public ContainerRegistration Registration { get; set; } = null!;
    public ContainerTrackingStatus Status { get; set; }
    public DateTimeOffset EventAt { get; set; }
    public string Location { get; set; } = "";
    public string? VesselName { get; set; }
    public string? Note { get; set; }
    public DateOnly ExpectedArrival { get; set; }
    public bool IsOnHold { get; set; }
    public ICollection<ContainerAttachment> Files { get; set; } = [];
}
