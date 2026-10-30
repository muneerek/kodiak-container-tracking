using System.ComponentModel.DataAnnotations;
using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record ContainerRequest(
    [Required, StringLength(20)] string ContainerType, [Required, StringLength(180)] string Cargo,
    [Range(typeof(decimal), "0.001", "100000")] decimal WeightKg, [Range(1, 1000000)] int Packages,
    [Range(1, int.MaxValue)] int OriginPortId, [Range(1, int.MaxValue)] int DestinationPortId,
    DateOnly Departure, DateOnly ExpectedArrival, [StringLength(24)] string? ContainerNumber = null,
    [StringLength(60)] string? Reference = null, [Range(1, int.MaxValue)] int? ShipperId = null);
public sealed record ContainerTrackingRequest(
    ContainerTrackingStatus Status, DateTimeOffset EventAt, [Required, StringLength(200)] string Location,
    DateOnly ExpectedArrival, [StringLength(24)] string? ContainerNumber = null,
    [StringLength(150)] string? VesselName = null, [StringLength(2000)] string? Note = null, bool IsOnHold = false);
public sealed record ContainerTrackingResponse(int Id, ContainerTrackingStatus Status, DateTimeOffset EventAt, string Location,
    string? VesselName, string? Note, DateOnly ExpectedArrival, bool IsOnHold, DateTimeOffset RecordedAt, string? RecordedBy, IReadOnlyList<ContainerFileResponse> Files);
public sealed record ContainerFileResponse(int Id, string FileName, ContainerFileKind Kind, string ContentType, long SizeBytes, DateTimeOffset UploadedOn, int? TrackingEventId = null);
public sealed record ContainerResponse(int Id, string Code, int ShipperId, string ShipperName, int? ConsigneeId, string? ConsigneeName,
    string? ContainerNumber, string ContainerType, string Cargo, decimal WeightKg, int Packages,
    int OriginPortId, string OriginPortName, int DestinationPortId, string DestinationPortName,
    DateOnly Departure, DateOnly ExpectedArrival, string? Reference, RegistrationStatus Status, string Version, DateTimeOffset CreatedOn, IReadOnlyList<ContainerFileResponse> Files, ContainerTrackingStatus TrackingStatus, bool IsOnHold, IReadOnlyList<ContainerTrackingResponse> TrackingEvents, string OriginPortCode, string DestinationPortCode);
internal static class ContainerMapper
{
    internal static ContainerResponse ToResponse(this ContainerRegistration x) => new(x.Id, x.Code, x.ShipperId, x.Shipper.Name, x.ConsigneeId, x.Consignee?.Name,
        x.ContainerNumber, x.ContainerType, x.Cargo, x.WeightKg, x.Packages, x.OriginPortId, x.OriginPort.Name, x.DestinationPortId, x.DestinationPort.Name,
        x.Departure, x.ExpectedArrival, x.Reference, x.Status, x.Version.ToString(), x.CreateOn,
        x.Attachments.Where(f => !f.IsDeleted).OrderBy(f => f.Id).Select(File).ToArray(), x.TrackingStatus, x.IsOnHold,
        x.TrackingEvents.OrderByDescending(e => e.EventAt).ThenByDescending(e => e.Id).Select(e => new ContainerTrackingResponse(e.Id, e.Status, e.EventAt, e.Location,
            e.VesselName, e.Note, e.ExpectedArrival, e.IsOnHold, e.CreateOn, e.CreatedBy,
            x.Attachments.Where(f => f.TrackingEventId == e.Id && !f.IsDeleted).OrderBy(f => f.Id).Select(File).ToArray())).ToArray(), x.OriginPort.Code, x.DestinationPort.Code);
    private static ContainerFileResponse File(ContainerAttachment f) => new(f.Id, f.FileName, f.Kind, f.ContentType, f.SizeBytes, f.CreateOn, f.TrackingEventId);
}
