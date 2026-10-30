using System.Text.RegularExpressions;
using Kodiak.ContainerTracking.Business.Configuration;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
using Microsoft.Extensions.Options;
namespace Kodiak.ContainerTracking.Business.Services;
public sealed record PreparedContainerFile(string RelativePath, string FileName, string ContentType, long SizeBytes);
public sealed record ContainerDownload(Stream Content, string ContentType);
public interface IContainerFileStorage
{
    Task<PreparedContainerFile> SaveAsync(Stream input, string fileName, ContainerFileKind kind, CancellationToken ct);
    Task<ContainerDownload?> OpenAsync(string path, string contentType, CancellationToken ct);
    Task DeleteAsync(string path, CancellationToken ct);
}
public interface IContainerService
{
    Task<PagedResponse<ContainerResponse>> ListAsync(int page, int size, string? search, RegistrationStatus? status, CancellationToken ct, ContainerTrackingStatus? trackingStatus = null, bool? onHold = null, int? shipperId = null);
    Task<ContainerResponse> GetAsync(int id, CancellationToken ct);
    Task<ContainerResponse> CreateAsync(ContainerRequest request, CancellationToken ct);
    Task<ContainerResponse> UpdateAsync(int id, long version, ContainerRequest request, CancellationToken ct);
    Task RequireTrackingAsync(int id, long version, CancellationToken ct);
    Task<ContainerResponse> UpdateTrackingAsync(int id, long version, ContainerTrackingRequest request, IReadOnlyList<PreparedContainerFile> files, CancellationToken ct);
    Task<ContainerResponse> CancelAsync(int id, long version, CancellationToken ct);
    Task<PagedResponse<ContainerOption>> OptionsAsync(string kind, int page, int size, CancellationToken ct);
    Task RequireUploadAsync(int id, long version, CancellationToken ct);
    Task<ContainerResponse> AddFileAsync(int id, long version, PreparedContainerFile file, ContainerFileKind kind, CancellationToken ct);
    Task<ContainerResponse> RemoveFileAsync(int id, int fileId, long version, CancellationToken ct);
    Task<ContainerAttachment> GetFileAsync(int id, int fileId, CancellationToken ct);
}
public sealed partial class ContainerService(IContainerRepository repository, IPartyRepository parties, IMasterRepository masters,
    IUserManagementRepository users, IUnitOfWork unitOfWork, ICurrentActor actor, IOptions<ContainerStorageOptions> storage) : IContainerService
{
    private async Task<ContainerScope> Scope(CancellationToken ct)
    {
        if (actor.IsAdmin) return new(true);
        var user = actor.UserId is long id ? await users.GetUserAsync(id, ct) : null;
        if (user is not { IsActive: true }) throw Forbidden();
        return user.UserType switch { UserType.Shipper => new(false, user.LinkedUniqueId), UserType.Consignee => new(false, null, user.LinkedUniqueId), _ => throw Forbidden() };
    }
    public async Task<PagedResponse<ContainerResponse>> ListAsync(int page, int size, string? search, RegistrationStatus? status, CancellationToken ct, ContainerTrackingStatus? trackingStatus = null, bool? onHold = null, int? shipperId = null)
    {
        ServiceValidation.Page(page, size);
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new BusinessException(400, "invalid_status", "Select a valid container status.");
        if (trackingStatus.HasValue && !Enum.IsDefined(trackingStatus.Value)) throw new BusinessException(400, "invalid_tracking_status", "Select a valid tracking status.");
        if (shipperId is <= 0) throw new BusinessException(400, "invalid_shipper", "Select a valid shipper.");
        if (search?.Length > 100) throw new BusinessException(400, "invalid_search", "Search is limited to 100 characters.");
        var (items, total) = await repository.ListAsync(await Scope(ct), page, size, search, status, ct, trackingStatus, onHold, shipperId); return new(items.Select(x => x.ToResponse()).ToArray(), total, page, size);
    }
    private async Task<ContainerRegistration> Find(int id, CancellationToken ct) => await repository.GetAsync(id, await Scope(ct), ct) ?? throw new BusinessException(404, "container_not_found", "Container registration was not found.");
    public async Task<ContainerResponse> GetAsync(int id, CancellationToken ct) => (await Find(id, ct)).ToResponse();
    private async Task Writable(ContainerRegistration item, long version, CancellationToken ct)
    {
        var scope = await Scope(ct); if (!scope.IsAdmin && scope.ShipperId != item.ShipperId) throw Forbidden();
        ServiceValidation.Version(version, item.Version);
        if (item.Status != RegistrationStatus.Booked) throw new BusinessException(409, "registration_closed", "Cancelled registrations cannot be changed.");
        var shipper = (await parties.GetReferenceAsync(item.ShipperId, ct))?.Shipper;
        if (shipper?.Status != BusinessStatus.Active) throw new BusinessException(409, "shipper_unavailable", "An active shipper is required.");
    }
    public Task<ContainerResponse> CreateAsync(ContainerRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request);
        return unitOfWork.InTransactionAsync(async () =>
        {
            var scope = await Scope(ct); if (!scope.IsAdmin && scope.ShipperId == null) throw Forbidden();
            var shipperId = scope.IsAdmin ? request.ShipperId : scope.ShipperId;
            if (shipperId == null) throw new BusinessException(400, "invalid_shipper", "Select an active shipper.");
            if (!scope.IsAdmin && request.ShipperId != null && request.ShipperId != scope.ShipperId) throw Forbidden();
            var shipper = (await parties.GetReferenceAsync(shipperId.Value, ct))?.Shipper;
            if (shipper?.Status != BusinessStatus.Active) throw new BusinessException(400, "invalid_shipper", "Select an active shipper.");
            var item = new ContainerRegistration { Shipper = shipper, ShipperId = shipper.UniqueId, ConsigneeId = shipper.ConsigneeId, Consignee = shipper.Consignee };
            await Apply(item, request, ct); item.Code = "KD-" + (await repository.NextNumberAsync(ct)).ToString("D6");
            repository.Add(item); await unitOfWork.SaveAsync(actor.UserId, actor.Subject, "Register container", ct); return item.ToResponse();
        }, ct);
    }
    public Task<ContainerResponse> UpdateAsync(int id, long version, ContainerRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request);
        return unitOfWork.InTransactionAsync(async () =>
        {
            var item = await Find(id, ct); await Writable(item, version, ct);
            if (request.ShipperId != null && request.ShipperId != item.ShipperId) throw new BusinessException(409, "immutable_shipper", "A registration cannot be moved to another shipper.");
            await Apply(item, request, ct); item.Version++; await unitOfWork.SaveAsync(actor.UserId, actor.Subject, "Edit container registration", ct); return item.ToResponse();
        }, ct);
    }
    public Task<ContainerResponse> CancelAsync(int id, long version, CancellationToken ct) => unitOfWork.InTransactionAsync(async () =>
    {
        if (!actor.IsAdmin) throw Forbidden(); var item = await Find(id, ct); ServiceValidation.Version(version, item.Version);
        item.Status = RegistrationStatus.Cancelled; item.Version++;
        await unitOfWork.SaveAsync(actor.UserId, actor.Subject, "Cancel container registration", ct); return item.ToResponse();
    }, ct);
    private async Task Apply(ContainerRegistration item, ContainerRequest request, CancellationToken ct)
    {
        if (request.ContainerType is not ("20STD" or "40STD" or "40HC" or "20REEFER" or "40REEFER")) throw new BusinessException(400, "invalid_type", "Select a supported container type.");
        if (request.OriginPortId == request.DestinationPortId) throw new BusinessException(400, "invalid_route", "Origin and destination ports must be different.");
        if (request.Departure == DateOnly.MinValue || request.ExpectedArrival == DateOnly.MinValue || request.ExpectedArrival < request.Departure) throw new BusinessException(400, "invalid_dates", "Arrival must be on or after departure; both dates are required.");
        async Task<ShippingPort> Port(int portId) => await masters.GetAsync(MasterKind.ShippingPorts, portId, ct) is ShippingPort { IsActive: true } port
            && await masters.GetAsync(MasterKind.Countries, port.CountryId, ct) is { IsActive: true }
            && (port.StateId == null || await masters.GetAsync(MasterKind.States, port.StateId.Value, ct) is { IsActive: true }) ? port : throw new BusinessException(400, "invalid_port", "Select active ports with active location records.");
        var number = ServiceValidation.Optional(request.ContainerNumber);
        if (number != null) { number = string.Concat(number.Where(char.IsLetterOrDigit)).ToUpperInvariant(); if (!ContainerNumberPattern().IsMatch(number)) throw new BusinessException(400, "invalid_container_number", "Use an 11-character container number, for example MSCU7823416, or leave it blank for allocation."); if (await repository.NumberExistsAsync(number, item.Id, ct)) throw new BusinessException(409, "container_in_use", "This container already has an active registration."); }
        item.OriginPort = await Port(request.OriginPortId); item.DestinationPort = await Port(request.DestinationPortId);
        item.OriginPortId = request.OriginPortId; item.DestinationPortId = request.DestinationPortId;
        item.ContainerNumber = number; item.ContainerType = request.ContainerType; item.Cargo = ServiceValidation.Required(request.Cargo, "Cargo description");
        item.WeightKg = request.WeightKg; item.Packages = request.Packages; item.Departure = request.Departure; item.ExpectedArrival = request.ExpectedArrival; item.Reference = ServiceValidation.Optional(request.Reference);
    }
    public async Task<PagedResponse<ContainerOption>> OptionsAsync(string kind, int page, int size, CancellationToken ct)
    {
        ServiceValidation.Page(page, size); if (kind is not ("ports" or "shippers" or "tracking-shippers")) throw new BusinessException(400, "invalid_options", "Unknown container options.");
        var (items, total) = await repository.OptionsAsync(kind, await Scope(ct), page, size, ct); return new(items, total, page, size);
    }
    public async Task RequireUploadAsync(int id, long version, CancellationToken ct) => await Writable(await Find(id, ct), version, ct);
    public Task<ContainerResponse> AddFileAsync(int id, long version, PreparedContainerFile file, ContainerFileKind kind, CancellationToken ct) => unitOfWork.InTransactionAsync(async () =>
    {
        if (!Enum.IsDefined(kind)) throw new BusinessException(400, "invalid_file_kind", "Choose photo or document.");
        var item = await Find(id, ct); await Writable(item, version, ct);
        if (item.Attachments.Count(x => !x.IsDeleted) >= storage.Value.MaxFilesPerContainer) throw new BusinessException(400, "file_limit", "This registration has reached its attachment limit.");
        item.Attachments.Add(new ContainerAttachment { FileName = file.FileName, RelativePath = file.RelativePath, ContentType = file.ContentType, SizeBytes = file.SizeBytes, Kind = kind });
        item.Version++; await unitOfWork.SaveAsync(actor.UserId, actor.Subject, "Upload container attachment", ct); return item.ToResponse();
    }, ct);
    public Task<ContainerResponse> RemoveFileAsync(int id, int fileId, long version, CancellationToken ct) => unitOfWork.InTransactionAsync(async () =>
    {
        var item = await Find(id, ct); await Writable(item, version, ct);
        var file = item.Attachments.SingleOrDefault(x => x.Id == fileId && !x.IsDeleted) ?? throw new BusinessException(404, "file_not_found", "Attachment was not found.");
        if (file.TrackingEventId != null) throw new BusinessException(409, "tracking_file_retained", "Tracking documents are retained with the shipment history.");
        file.IsDeleted = true; item.Version++; await unitOfWork.SaveAsync(actor.UserId, actor.Subject, "Remove container attachment", ct); return item.ToResponse();
    }, ct);
    public async Task<ContainerAttachment> GetFileAsync(int id, int fileId, CancellationToken ct) => (await Find(id, ct)).Attachments.SingleOrDefault(x => x.Id == fileId && !x.IsDeleted) ?? throw new BusinessException(404, "file_not_found", "Attachment was not found.");
    [GeneratedRegex(@"^[A-Z]{3}[UJZ][0-9]{7}$", RegexOptions.CultureInvariant)] private static partial Regex ContainerNumberPattern();
    private static BusinessException Forbidden() => new(403, "forbidden", "You do not have permission for this container action.");
}
