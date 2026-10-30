using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kodiak.ContainerTracking.Api.Controllers;

[Authorize(Policy = "UsersManage")]
public abstract class CompanyControllerBase(IPartyService service, PartyType type) : ApiControllerBase
{
    protected IPartyService Service { get; } = service;

    [HttpGet]
    public async Task<ActionResult<PagedResponse<PartyResponse>>> List(CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null) =>
        Ok(await Service.ListAsync(type, page, pageSize, ct, search));

    [HttpGet("{id:long:min(1)}")]
    public async Task<ActionResult<PartyResponse>> Get(long id, CancellationToken ct)
    {
        var company = await Service.GetAsync(type, id, ct);
        SetEtag(company.Version);
        return Ok(company);
    }

    [HttpPost]
    public async Task<ActionResult<PartyResponse>> Create(CompanyRequest request, CancellationToken ct)
    {
        var company = await Service.CreateCompanyAsync(type, request, ct);
        SetEtag(company.Version);
        return CreatedAtAction(nameof(Get), new { id = company.UniqueId }, company);
    }

    [HttpPut("{id:long:min(1)}")]
    public async Task<ActionResult<PartyResponse>> Update(long id, CompanyRequest request, CancellationToken ct)
    {
        var company = await Service.UpdateCompanyAsync(type, id, ExpectedVersion(), request, ct);
        SetEtag(company.Version);
        return Ok(company);
    }
}
