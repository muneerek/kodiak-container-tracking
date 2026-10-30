using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
namespace Kodiak.ContainerTracking.Business.Services;

public sealed class ShipperAccess(IUserManagementRepository users, ICurrentActor actor)
{
    public void RequireAdmin() { if (!actor.IsAdmin) throw new BusinessException(403, "forbidden", "Administrator access is required."); }
    public async Task<int> OwnIdAsync(CancellationToken ct)
    {
        var user = actor.IsShipper && actor.UserId is long id ? await users.GetUserAsync(id, ct) : null;
        if (user is not { UserType: UserType.Shipper, IsActive: true }) throw new BusinessException(403, "forbidden", "Shipper access is required.");
        return user.LinkedUniqueId;
    }
    public async Task RequireAsync(long id, CancellationToken ct)
    {
        if (!actor.IsAdmin && await OwnIdAsync(ct) != id) throw new BusinessException(404, "shipper_not_found", "Shipper was not found.");
    }
}
