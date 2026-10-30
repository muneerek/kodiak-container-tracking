using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Kodiak.ContainerTracking.Api.Controllers;

[Route("api/consignees")]
public sealed class ConsigneesController(IPartyService service) : CompanyControllerBase(service, PartyType.Consignee)
{
    [HttpDelete("{id:long:min(1)}")]
    public async Task<ActionResult<PartyResponse>> Delete(long id, CancellationToken ct)
    {
        var consignee = await Service.DeactivateConsigneeAsync(id, ExpectedVersion(), ct);
        SetEtag(consignee.Version); return Ok(consignee);
    }
}
