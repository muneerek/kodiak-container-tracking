using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/registration")]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ShipperRegistrationController(IShipperRegistrationService service) : ApiControllerBase
{
    [HttpPost("shippers"), EnableRateLimiting("registration")]
    public async Task<ActionResult<ShipperRegistrationResponse>> Register(ShipperRegistrationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.RegisterAsync(request, ct));

    [HttpGet("options/{kind}"), EnableRateLimiting("registration-options")]
    public async Task<ActionResult<PagedResponse<RegistrationOption>>> Options(string kind, CancellationToken ct,
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        Ok(await service.OptionsAsync(kind, search, page, pageSize, ct));
}
