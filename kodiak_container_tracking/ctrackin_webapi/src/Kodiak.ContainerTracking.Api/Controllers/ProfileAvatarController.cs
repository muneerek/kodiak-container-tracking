using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Kodiak.ContainerTracking.Api.Controllers;
[Route("api/profile/avatar"), Authorize]
public sealed class ProfileAvatarController(IUserManagementRepository users, IEmployeePhotoStorage storage, ICurrentActor actor) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var user = actor.UserId is long id ? await users.GetUserAsync(id, ct) : null;
        if (user is not { IsActive: true }) return Unauthorized();
        var path = user.UserType switch {
            UserType.Admin or UserType.Normal => user.Reference.Employee?.Logo,
            UserType.Shipper => user.Reference.Shipper?.Logo,
            UserType.Consignee => user.Reference.Consignee?.Logo,
            _ => null
        };
        if (string.IsNullOrEmpty(path)) return NotFound();
        var image = await storage.OpenAsync(path, ct);
        if (image == null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(image.Content, image.ContentType);
    }
}

