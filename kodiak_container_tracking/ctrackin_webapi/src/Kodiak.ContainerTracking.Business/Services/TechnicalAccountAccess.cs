using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
namespace Kodiak.ContainerTracking.Business.Services;

public sealed class TechnicalAccountAccess(IUserManagementRepository users, ICurrentActor actor)
{
    public async Task RequireEmployeeAsync(long id, CancellationToken ct)
    {
        if (!actor.IsSuperAdmin && await users.IsProtectedEmployeeAsync(id, ct)) throw Hidden();
    }
    public async Task RequireUserAsync(ApplicationUser user, CancellationToken ct)
    {
        if (!actor.IsSuperAdmin && user.RoleMappings.Any(m => m.Role.NormalizedCode == "SUPERADMIN")) throw Hidden();
        await RequireEmployeeAsync(user.LinkedUniqueId, ct);
    }
    private static BusinessException Hidden() => new(404, "record_not_found", "Record was not found.");
}
