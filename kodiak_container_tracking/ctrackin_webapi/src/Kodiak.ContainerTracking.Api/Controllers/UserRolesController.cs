using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/users/{id:long:min(1)}/roles")]
[Authorize(Policy = "UsersManage")]
[Authorize(Policy = "AccessManage")]
public sealed class UserRolesController(IUserService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> Get(long id, CancellationToken ct)
    {
        var user = await service.GetAsync(id, ct);
        SetEtag(user.Version);
        return Ok(user.Roles);
    }

    [HttpPut]
    public async Task<ActionResult<UserResponse>> Replace(long id, ReplaceUserRolesRequest request, CancellationToken ct)
    {
        var user = await service.ReplaceRolesAsync(id, ExpectedVersion(), request, ct);
        SetEtag(user.Version);
        return Ok(user);
    }
}
