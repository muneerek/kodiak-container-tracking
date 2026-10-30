using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Kodiak.ContainerTracking.Api.Controllers;
[Route("api/dashboard/overview")]
[Authorize(Policy = "ContainersAccess")]
public sealed class OverviewController(IOverviewService service) : ApiControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<OverviewResponse>> Get(CancellationToken ct,[FromQuery] DateOnly? date = null) => Ok(await service.GetAsync(date,ct));
}
