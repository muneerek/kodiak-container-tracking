using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/parties")]
[Authorize(Policy = "UsersManage")]
public sealed class PartiesController(IPartyService service) : ApiControllerBase
{
    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<PartyResponse>> GetByCode(string code, CancellationToken ct)
    {
        var party = await service.GetByCodeAsync(code, ct);
        SetEtag(party.Version);
        return Ok(party);
    }
}
