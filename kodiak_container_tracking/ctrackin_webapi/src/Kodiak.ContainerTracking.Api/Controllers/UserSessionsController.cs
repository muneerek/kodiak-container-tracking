using Kodiak.ContainerTracking.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/users/{id:int:min(1)}")]
[Authorize(Policy = "UsersManage")]
public sealed class UserSessionsController(IUserSessionsService service) : ApiControllerBase
{
    [HttpGet("logins")]
    public async Task<IActionResult> Logins(int id, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        Ok(await service.LoginsAsync(id, page, pageSize, ct));
    [HttpGet("tokens")]
    [Authorize(Policy = "AccessManage")]
    public async Task<IActionResult> Tokens(int id, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        Ok(await service.TokensAsync(id, page, pageSize, ct));
    [HttpPost("revoke-sessions")]
    [Authorize(Policy = "AccessManage")]
    public async Task<IActionResult> Revoke(int id, RevokeSessionsRequest request, CancellationToken ct)
    { await service.RevokeAsync(id, ExpectedVersion(), request, ct); return NoContent(); }
}
