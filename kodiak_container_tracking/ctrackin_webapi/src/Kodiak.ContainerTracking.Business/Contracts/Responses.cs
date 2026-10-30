using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record RoleResponse(int RoleId, string Code, string RoleName, string? Description, bool IsActive, string Version);
public sealed record UserResponse(int AppUserId, string UserName, UserType UserType, int LinkedUniqueId, string LinkedUniqueCode,
    bool IsActive, PartyResponse Details, string Version, IReadOnlyList<RoleResponse> Roles);
internal static class ResponseMapper
{
    internal static RoleResponse ToResponse(this UserRole role) => new(role.RoleId, role.Code, role.RoleName, role.Description, role.IsActive, role.Version.ToString());
    internal static UserResponse ToResponse(this ApplicationUser user) => new(user.AppUserId, user.UserName, user.UserType, user.LinkedUniqueId,
        user.LinkedUniqueCode, user.IsActive, (user.Reference.GetParty() ?? throw new InvalidOperationException("Linked record is missing.")).ToResponse(),
        user.Version.ToString(), user.RoleMappings.OrderBy(x => x.FkRoleId).Select(x => x.Role.ToResponse()).ToArray());
}
