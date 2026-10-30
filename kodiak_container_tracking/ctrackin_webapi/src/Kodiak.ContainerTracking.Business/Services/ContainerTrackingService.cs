using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Business.Services;
public sealed partial class ContainerService
{
    public async Task RequireTrackingAsync(int id, long version, CancellationToken ct)
    {
        if (!actor.IsAdmin) throw Forbidden();
        await Writable(await Find(id, ct), version, ct);
    }
    public Task<ContainerResponse> UpdateTrackingAsync(int id, long version, ContainerTrackingRequest request, IReadOnlyList<PreparedContainerFile> files, CancellationToken ct)
    {
        ServiceValidation.Validate(request);
        if (!actor.IsAdmin) throw Forbidden();
        return unitOfWork.InTransactionAsync(async () =>
        {
            var item = await Find(id, ct); await Writable(item, version, ct);
            if (!Enum.IsDefined(request.Status)) throw new BusinessException(400, "invalid_tracking_status", "Select a valid tracking status.");
            if (request.EventAt == default || request.EventAt > DateTimeOffset.UtcNow.AddMinutes(5))
                throw new BusinessException(400, "invalid_event_time", "Enter an event time that is not in the future.");
            if (item.TrackingEvents.Any(e => e.EventAt > request.EventAt))
                throw new BusinessException(400, "invalid_event_order", "Event time must be on or after the latest tracking update.");
            if (request.ExpectedArrival == default || request.ExpectedArrival < item.Departure)
                throw new BusinessException(400, "invalid_arrival", "Expected arrival must be on or after departure.");
            if (request.IsOnHold && request.Status == ContainerTrackingStatus.Delivered)
                throw new BusinessException(409, "container_on_hold", "Release the hold before marking the container delivered.");
            if (item.Attachments.Count(f => !f.IsDeleted) + files.Count > storage.Value.MaxFilesPerContainer)
                throw new BusinessException(400, "file_limit", "This update exceeds the container attachment limit.");
            var number = ServiceValidation.Optional(request.ContainerNumber) ?? item.ContainerNumber;
            if (number != null)
            {
                number = string.Concat(number.Where(char.IsLetterOrDigit)).ToUpperInvariant();
                if (!ContainerNumberPattern().IsMatch(number)) throw new BusinessException(400, "invalid_container_number", "Enter a valid 11-character container number.");
                if (await repository.NumberExistsAsync(number, item.Id, ct)) throw new BusinessException(409, "container_in_use", "This container already has an active registration.");
            }
            var entry = new ContainerTrackingEvent { Status = request.Status, EventAt = request.EventAt.ToUniversalTime(),
                Location = ServiceValidation.Required(request.Location, "Reported location"), VesselName = ServiceValidation.Optional(request.VesselName),
                Note = ServiceValidation.Optional(request.Note), ExpectedArrival = request.ExpectedArrival, IsOnHold = request.IsOnHold };
            item.TrackingEvents.Add(entry);
            foreach (var file in files)
                item.Attachments.Add(new ContainerAttachment { TrackingEvent = entry, FileName = file.FileName, RelativePath = file.RelativePath,
                    ContentType = file.ContentType, SizeBytes = file.SizeBytes, Kind = file.ContentType.StartsWith("image/", StringComparison.Ordinal) ? ContainerFileKind.Photo : ContainerFileKind.Document });
            item.ContainerNumber = number; item.TrackingStatus = request.Status; item.IsOnHold = request.IsOnHold;
            item.ExpectedArrival = request.ExpectedArrival; item.Version++;
            await unitOfWork.SaveAsync(actor.UserId, actor.Subject, "Update container tracking", ct); return item.ToResponse();
        }, ct);
    }
}
