using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Kodiak.ContainerTracking.Api.Controllers;
[Route("api/profile/company")]
[Authorize(Policy="ContainersAccess")]
public sealed class CompanyProfileController(ICompanyProfileService service):ApiControllerBase
{
    [HttpGet]public async Task<ActionResult<PartyResponse>> Get(CancellationToken ct){var item=await service.GetAsync(ct);SetEtag(item.Version);return Ok(item);}
    [HttpPut]public async Task<ActionResult<PartyResponse>> Update(CompanyProfileRequest request,CancellationToken ct){var item=await service.UpdateAsync(ExpectedVersion(),request,ct);SetEtag(item.Version);return Ok(item);}
}
