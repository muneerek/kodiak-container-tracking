using System.Security.Claims;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthenticationController(IAuthenticationService service) : ApiControllerBase
{
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthenticationResponse>> Login(LoginRequest request, CancellationToken ct) => Ok(await service.LoginAsync(request, ct));
    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthenticationResponse>> Refresh(RefreshRequest request, CancellationToken ct) => Ok(await service.RefreshAsync(request, ct));
    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue("sub"), out var id)) return Unauthorized();
        await service.LogoutAsync(id, ct); return NoContent();
    }
}
