using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/users")]
[Authorize(Policy = "UsersManage")]
public sealed class UsersController(IUserService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<UserResponse>>> List(CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? uniqueCode = null) =>
        Ok(await service.ListAsync(page, pageSize, search, uniqueCode, ct));

    [HttpGet("{id:long:min(1)}")]
    public async Task<ActionResult<UserResponse>> Get(long id, CancellationToken ct)
    {
        var user = await service.GetAsync(id, ct);
        SetEtag(user.Version);
        return Ok(user);
    }

    [HttpPost]
    [Authorize(Policy = "AccessManage")]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await service.CreateAsync(request, ct);
        SetEtag(user.Version);
        return CreatedAtAction(nameof(Get), new { id = user.AppUserId }, user);
    }

    [HttpPut("{id:long:min(1)}")]
    public async Task<ActionResult<UserResponse>> Update(long id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await service.UpdateAsync(id, ExpectedVersion(), request, ct);
        SetEtag(user.Version);
        return Ok(user);
    }

    [HttpPost("{id:long:min(1)}/password")]
    [Authorize(Policy = "AccessManage")]
    public async Task<ActionResult<UserResponse>> SetPassword(long id, SetPasswordRequest request, CancellationToken ct)
    {
        var user = await service.SetPasswordAsync(id, ExpectedVersion(), request, ct); SetEtag(user.Version); return Ok(user);
    }
}
