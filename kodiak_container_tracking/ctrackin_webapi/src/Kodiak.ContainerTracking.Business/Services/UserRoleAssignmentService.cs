using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;

namespace Kodiak.ContainerTracking.Business.Services;

// Shared by create-user and dedicated role replacement. Caller owns the transaction.
public sealed class UserRoleAssignmentService(IUserManagementRepository repository, ICurrentActor actor)
{
    public async Task ApplyAsync(ApplicationUser user, int[] roleIds, CancellationToken ct)
    {
        if (roleIds.Any(x => x <= 0) || roleIds.Distinct().Count() != roleIds.Length)
            throw new BusinessException(400, "invalid_role_ids", "Role IDs must be positive and cannot contain duplicates.");

        var roles = await repository.GetRolesByIdsAsync(roleIds, ct);
        if (roles.Count != roleIds.Length)
            throw new BusinessException(400, "incompatible_roles", "Every role must exist.");

        if (roles.Any(x => x.NormalizedCode == "SUPERADMIN") && !actor.IsSuperAdmin)
            throw new BusinessException(403, "protected_role", "Only a superadmin can assign the SUPERADMIN role.");
        if (user.RoleMappings.Any(x => x.Role.NormalizedCode == "SUPERADMIN" && !roleIds.Contains(x.FkRoleId)))
            throw new BusinessException(409, "protected_role", "The technical account SUPERADMIN role cannot be removed.");
        var existingIds = user.RoleMappings.Select(x => x.FkRoleId).ToHashSet();
        if (roles.Any(x => !x.IsActive && !existingIds.Contains(x.RoleId)))
            throw new BusinessException(409, "inactive_role", "Inactive roles cannot be newly assigned.");

        foreach (var mapping in user.RoleMappings.Where(x => !roleIds.Contains(x.FkRoleId)).ToArray())
        {
            repository.RemoveMapping(mapping);
            user.RoleMappings.Remove(mapping);
        }
        foreach (var role in roles.Where(x => !existingIds.Contains(x.RoleId)))
            user.RoleMappings.Add(new UserRoleMapping { User = user, Role = role, FkRoleId = role.RoleId });
    }
}
