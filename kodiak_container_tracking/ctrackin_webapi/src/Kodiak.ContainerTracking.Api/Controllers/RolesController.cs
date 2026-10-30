using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/roles")]
[Authorize(Policy = "AccessManage")]
public sealed class RolesController(IRoleService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<RoleResponse>>> List(CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        Ok(await service.ListAsync(page, pageSize, ct));

    [HttpGet("{id:int:min(1)}")]
    public async Task<ActionResult<RoleResponse>> Get(int id, CancellationToken ct)
    {
        var role = await service.GetAsync(id, ct);
        SetEtag(role.Version);
        return Ok(role);
    }

    [HttpPost]
    public async Task<ActionResult<RoleResponse>> Create(CreateRoleRequest request, CancellationToken ct)
    {
        var role = await service.CreateAsync(request, ct);
        SetEtag(role.Version);
        return CreatedAtAction(nameof(Get), new { id = role.RoleId }, role);
    }

    [HttpPut("{id:int:min(1)}")]
    public async Task<ActionResult<RoleResponse>> Update(int id, UpdateRoleRequest request, CancellationToken ct)
    {
        var role = await service.UpdateAsync(id, ExpectedVersion(), request, ct);
        SetEtag(role.Version);
        return Ok(role);
    }
}
