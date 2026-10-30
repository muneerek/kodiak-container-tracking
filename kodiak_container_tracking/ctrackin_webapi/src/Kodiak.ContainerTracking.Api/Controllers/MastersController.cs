using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/masters/{kind}")]
[Authorize(Policy = "UsersManage")]
public sealed class MastersController(IMasterService service) : ApiControllerBase
{
    [HttpGet("/api/masters/ShippingPorts/next-code")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> NextShippingPortCode(CancellationToken ct) => Ok(new { code = await service.PreviewShippingPortCodeAsync(ct) });
    [HttpGet("/api/masters/timezones")]
    public async Task<ActionResult<IReadOnlyList<TimeZoneResponse>>> TimeZones(CancellationToken ct) => Ok(await service.ListTimeZonesAsync(ct));
    [HttpGet]
    public async Task<ActionResult<PagedResponse<MasterResponse>>> List(MasterKind kind, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        Ok(await service.ListAsync(kind, page, pageSize, ct));
    [HttpGet("{id:int:min(1)}")]
    public async Task<ActionResult<MasterResponse>> Get(MasterKind kind, int id, CancellationToken ct)
    { var record = await service.GetAsync(kind, id, ct); SetEtag(record.Version); return Ok(record); }
    [HttpPost]
    public async Task<ActionResult<MasterResponse>> Create(MasterKind kind, MasterRequest request, CancellationToken ct)
    { var record = await service.CreateAsync(kind, request, ct); SetEtag(record.Version); return CreatedAtAction(nameof(Get), new { kind, id = record.Id }, record); }
    [HttpDelete("{id:int:min(1)}")]
    public async Task<ActionResult<MasterResponse>> Delete(MasterKind kind, int id, CancellationToken ct)
    { var record = await service.DeactivateAsync(kind, id, ExpectedVersion(), ct); SetEtag(record.Version); return Ok(record); }
    [HttpPut("{id:int:min(1)}")]
    public async Task<ActionResult<MasterResponse>> Update(MasterKind kind, int id, MasterRequest request, CancellationToken ct)
    { var record = await service.UpdateAsync(kind, id, ExpectedVersion(), request, ct); SetEtag(record.Version); return Ok(record); }
}
