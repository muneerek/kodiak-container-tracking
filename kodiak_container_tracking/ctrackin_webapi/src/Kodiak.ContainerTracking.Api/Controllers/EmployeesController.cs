using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/employees")]
[Authorize(Policy = "UsersManage")]
public sealed class EmployeesController(IPartyService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<PartyResponse>>> List(CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        Ok(await service.ListAsync(PartyType.Employee, page, pageSize, ct));

    [HttpGet("{id:long:min(1)}")]
    public async Task<ActionResult<PartyResponse>> Get(long id, CancellationToken ct)
    {
        var employee = await service.GetAsync(PartyType.Employee, id, ct);
        SetEtag(employee.Version);
        return Ok(employee);
    }

    [HttpPost]
    public async Task<ActionResult<PartyResponse>> Create(EmployeeRequest request, CancellationToken ct)
    {
        var employee = await service.CreateEmployeeAsync(request, ct);
        SetEtag(employee.Version);
        return CreatedAtAction(nameof(Get), new { id = employee.UniqueId }, employee);
    }

    // Preserve linked accounts and audit history by terminating rather than physically deleting.
    [HttpDelete("{id:long:min(1)}")]
    public async Task<ActionResult<PartyResponse>> Delete(long id, CancellationToken ct)
    {
        var employee = await service.TerminateEmployeeAsync(id, ExpectedVersion(), ct);
        SetEtag(employee.Version);
        return Ok(employee);
    }

    [HttpPut("{id:long:min(1)}")]
    public async Task<ActionResult<PartyResponse>> Update(long id, EmployeeRequest request, CancellationToken ct)
    {
        var employee = await service.UpdateEmployeeAsync(id, ExpectedVersion(), request, ct);
        SetEtag(employee.Version);
        return Ok(employee);
    }
}
