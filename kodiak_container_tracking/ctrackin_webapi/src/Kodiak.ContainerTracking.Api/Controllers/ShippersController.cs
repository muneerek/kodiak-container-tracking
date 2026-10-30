using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/shippers")]
[Authorize(Policy = "ShippersManage")]
public sealed class ShippersController(IPartyService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<PartyResponse>>> List(CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null) =>
        Ok(await service.ListAsync(PartyType.Shipper, page, pageSize, ct, search));

    [HttpGet("{id:long:min(1)}")]
    public async Task<ActionResult<PartyResponse>> Get(long id, CancellationToken ct)
    {
        var shipper = await service.GetAsync(PartyType.Shipper, id, ct);
        SetEtag(shipper.Version);
        return Ok(shipper);
    }

    [Authorize(Policy = "UsersManage")]
    [HttpPost]
    public async Task<ActionResult<PartyResponse>> Create(ShipperRequest request, CancellationToken ct)
    {
        var shipper = await service.CreateShipperAsync(request, ct);
        SetEtag(shipper.Version);
        return CreatedAtAction(nameof(Get), new { id = shipper.UniqueId }, shipper);
    }

    [Authorize(Policy = "UsersManage")]
    [HttpDelete("{id:long:min(1)}")]
    public async Task<ActionResult<PartyResponse>> Delete(long id, CancellationToken ct)
    {
        var shipper = await service.DeactivateShipperAsync(id, ExpectedVersion(), ct);
        SetEtag(shipper.Version); return Ok(shipper);
    }

    [HttpPut("{id:long:min(1)}")]
    public async Task<ActionResult<PartyResponse>> Update(long id, ShipperRequest request, CancellationToken ct)
    {
        var shipper = await service.UpdateShipperAsync(id, ExpectedVersion(), request, ct);
        SetEtag(shipper.Version);
        return Ok(shipper);
    }
}
