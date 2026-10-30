using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/employees/{id:int:min(1)}/photo")]
[Authorize(Policy = "UsersManage")]
public sealed class EmployeePhotosController(IPartyService employees, IEmployeePhotoStorage storage) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var employee = await employees.GetAsync(Kodiak.ContainerTracking.DataAccess.Entities.PartyType.Employee, id, ct);
        if (employee.Logo?.StartsWith("employees/", StringComparison.Ordinal) != true) return NotFound();
        var photo = await storage.OpenAsync(employee.Logo, ct);
        if (photo == null) return NotFound();
        Response.Headers.CacheControl = "private, no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(photo.Content, photo.ContentType);
    }
    [HttpPut]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<ActionResult<PartyResponse>> Upload(int id, IFormFile photo, CancellationToken ct)
    {
        var version = ExpectedVersion();
        await employees.GetAsync(Kodiak.ContainerTracking.DataAccess.Entities.PartyType.Employee, id, ct);
        await using var input = photo.OpenReadStream();
        var path = await storage.SaveAsync(input, ct);
        try
        {
            var employee = await employees.SetEmployeePhotoAsync(id, version, path, ct);
            SetEtag(employee.Version); return Ok(employee);
        }
        catch { await storage.DeleteAsync(path, CancellationToken.None); throw; }
    }
    [HttpDelete]
    public async Task<ActionResult<PartyResponse>> Remove(int id, CancellationToken ct)
    {
        var employee = await employees.SetEmployeePhotoAsync(id, ExpectedVersion(), null, ct);
        SetEtag(employee.Version); return Ok(employee);
    }
}
