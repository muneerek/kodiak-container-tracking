using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Kodiak.ContainerTracking.Api.Controllers;
[Route("api/containers")]
[Authorize(Policy = "ContainersAccess")]
public sealed class ContainersController(IContainerService service, IContainerFileStorage storage) : ApiControllerBase
{
    [HttpGet] public async Task<ActionResult<PagedResponse<ContainerResponse>>> List(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] RegistrationStatus? status = null) => Ok(await service.ListAsync(page, pageSize, search, status, ct));
    [HttpGet("tracking")]
    public async Task<ActionResult<PagedResponse<ContainerResponse>>> TrackingList(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 12,
        [FromQuery] string? search = null, [FromQuery] ContainerTrackingStatus? trackingStatus = null, [FromQuery] bool? onHold = null,
        [FromQuery] int? shipperId = null, [FromQuery] RegistrationStatus? status = null) =>
        Ok(await service.ListAsync(page, pageSize, search, status, ct, trackingStatus, onHold, shipperId));
    [HttpGet("{id:int:min(1)}")] public async Task<ActionResult<ContainerResponse>> Get(int id, CancellationToken ct) { var item = await service.GetAsync(id, ct); SetEtag(item.Version); return Ok(item); }
    [HttpGet("options/{kind}")] public async Task<ActionResult<PagedResponse<ContainerOption>>> Options(string kind, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 100) => Ok(await service.OptionsAsync(kind, page, pageSize, ct));
    [HttpPost] public async Task<ActionResult<ContainerResponse>> Create(ContainerRequest request, CancellationToken ct) { var item = await service.CreateAsync(request, ct); SetEtag(item.Version); return CreatedAtAction(nameof(Get), new { id = item.Id }, item); }
    [HttpPut("{id:int:min(1)}")] public async Task<ActionResult<ContainerResponse>> Update(int id, ContainerRequest request, CancellationToken ct) { var item = await service.UpdateAsync(id, ExpectedVersion(), request, ct); SetEtag(item.Version); return Ok(item); }
    [HttpDelete("{id:int:min(1)}"), Authorize(Policy = "UsersManage")] public async Task<ActionResult<ContainerResponse>> Cancel(int id, CancellationToken ct) { var item = await service.CancelAsync(id, ExpectedVersion(), ct); SetEtag(item.Version); return Ok(item); }
    [HttpPost("{id:int:min(1)}/tracking"), Authorize(Policy = "UsersManage"), Consumes("multipart/form-data")]
    [RequestSizeLimit(52 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 52 * 1024 * 1024)]
    public async Task<ActionResult<ContainerResponse>> Tracking(int id, [FromForm] ContainerTrackingRequest request, IFormFileCollection? files, CancellationToken ct)
    {
        var version = ExpectedVersion(); await service.RequireTrackingAsync(id, version, ct);
        var uploads = files?.ToArray() ?? [];
        if (uploads.Length > 10 || uploads.Sum(f => f.Length) > 50L * 1024 * 1024)
            return BadRequest(new { message = "Attach up to 10 files with a combined size of 50 MB per update." });
        var saved = new List<PreparedContainerFile>();
        try
        {
            foreach (var file in uploads)
            {
                await using var input = file.OpenReadStream();
                saved.Add(await storage.SaveAsync(input, file.FileName, ContainerFileKind.Document, ct));
            }
            var item = await service.UpdateTrackingAsync(id, version, request, saved, ct);
            SetEtag(item.Version); return Ok(item);
        }
        catch
        {
            foreach (var file in saved) await storage.DeleteAsync(file.RelativePath, CancellationToken.None);
            throw;
        }
    }
    [HttpPost("{id:int:min(1)}/files"), RequestSizeLimit(12 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
    public async Task<ActionResult<ContainerResponse>> Upload(int id, IFormFile file, [FromForm] ContainerFileKind kind, CancellationToken ct)
    {
        var version = ExpectedVersion(); await service.RequireUploadAsync(id, version, ct);
        await using var input = file.OpenReadStream(); var saved = await storage.SaveAsync(input, file.FileName, kind, ct);
        try { var item = await service.AddFileAsync(id, version, saved, kind, ct); SetEtag(item.Version); return Ok(item); }
        catch { await storage.DeleteAsync(saved.RelativePath, CancellationToken.None); throw; }
    }
    [HttpGet("{id:int:min(1)}/files/{fileId:int:min(1)}")]
    public async Task<IActionResult> Download(int id, int fileId, CancellationToken ct)
    {
        var file = await service.GetFileAsync(id, fileId, ct); var result = await storage.OpenAsync(file.RelativePath, file.ContentType, ct); if (result == null) return NotFound();
        Response.Headers.CacheControl = "private, no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return file.Kind == ContainerFileKind.Photo ? File(result.Content, result.ContentType) : File(result.Content, result.ContentType, file.FileName);
    }
    [HttpDelete("{id:int:min(1)}/files/{fileId:int:min(1)}")]
    public async Task<ActionResult<ContainerResponse>> Remove(int id, int fileId, CancellationToken ct) { var item = await service.RemoveFileAsync(id, fileId, ExpectedVersion(), ct); SetEtag(item.Version); return Ok(item); }
}
