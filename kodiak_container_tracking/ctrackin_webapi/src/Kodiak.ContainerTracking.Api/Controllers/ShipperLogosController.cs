using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/shippers/{id:int:min(1)}/logo")]
[Authorize(Policy = "ShippersManage")]
public sealed class ShipperLogosController(IPartyService shippers, IEmployeePhotoStorage storage) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var shipper = await shippers.GetAsync(Kodiak.ContainerTracking.DataAccess.Entities.PartyType.Shipper, id, ct);
        if (shipper.Logo?.StartsWith("shippers/", StringComparison.Ordinal) != true) return NotFound();
        var photo = await storage.OpenAsync(shipper.Logo, ct);
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
        await shippers.GetAsync(Kodiak.ContainerTracking.DataAccess.Entities.PartyType.Shipper, id, ct);
        await using var input = photo.OpenReadStream();
        var path = await storage.SaveAsync(input, ct, "shippers");
        try
        {
            var shipper = await shippers.SetShipperLogoAsync(id, version, path, ct);
            SetEtag(shipper.Version); return Ok(shipper);
        }
        catch { await storage.DeleteAsync(path, CancellationToken.None); throw; }
    }
    [HttpDelete]
    public async Task<ActionResult<PartyResponse>> Remove(int id, CancellationToken ct)
    {
        var shipper = await shippers.SetShipperLogoAsync(id, ExpectedVersion(), null, ct);
        SetEtag(shipper.Version); return Ok(shipper);
    }
}
