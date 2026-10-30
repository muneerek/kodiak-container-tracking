using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/masters/BusinessCategories")]
[Authorize(Policy = "UsersManage")]
public sealed class BusinessCategoriesController(IMasterService service) : ApiControllerBase
{
    [HttpDelete("{id:int:min(1)}")]
    public async Task<ActionResult<MasterResponse>> Delete(int id, CancellationToken ct)
    {
        var category = await service.DeactivateBusinessCategoryAsync(id, ExpectedVersion(), ct);
        SetEtag(category.Version); return Ok(category);
    }
}
