namespace Kodiak.ContainerTracking.DataAccess.Entities;
public enum RegistrationStatus { Booked = 1, Cancelled = 2 }
public enum ContainerFileKind { Photo = 1, Document = 2 }
public sealed class ContainerRegistration : AuditableEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int ShipperId { get; set; }
    public int? ConsigneeId { get; set; }
    public string? ContainerNumber { get; set; }
    public string ContainerType { get; set; } = "40HC";
    public string Cargo { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public int Packages { get; set; }
    public int OriginPortId { get; set; }
    public int DestinationPortId { get; set; }
    public DateOnly Departure { get; set; }
    public DateOnly ExpectedArrival { get; set; }
    public string? Reference { get; set; }
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Booked;
    public long Version { get; set; } = 1;
    public Shipper Shipper { get; set; } = null!;
    public Consignee? Consignee { get; set; }
    public ShippingPort OriginPort { get; set; } = null!;
    public ShippingPort DestinationPort { get; set; } = null!;
    public ContainerTrackingStatus TrackingStatus { get; set; } = ContainerTrackingStatus.Booked;
    public bool IsOnHold { get; set; }
    public ICollection<ContainerTrackingEvent> TrackingEvents { get; set; } = [];
    public ICollection<ContainerAttachment> Attachments { get; set; } = [];
}
public sealed class ContainerAttachment : AuditableEntity
{
    public int Id { get; set; }
    public int ContainerRegistrationId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? TrackingEventId { get; set; }
    public ContainerTrackingEvent? TrackingEvent { get; set; }
    public ContainerFileKind Kind { get; set; }
    public bool IsDeleted { get; set; }
    public ContainerRegistration Registration { get; set; } = null!;
}
