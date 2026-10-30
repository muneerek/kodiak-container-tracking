using Kodiak.ContainerTracking.DataAccess.Entities;

namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public interface IUserManagementRepository
{
    Task<(IReadOnlyList<ApplicationUser> Items, int Total)> GetUsersAsync(int page, int pageSize, string? search, string? uniqueCode, CancellationToken ct, bool includeSuperAdmin = true);
    Task<ApplicationUser?> GetUserAsync(long id, CancellationToken ct);
    Task<(IReadOnlyList<UserRole> Items, int Total)> GetRolesAsync(int page, int pageSize, CancellationToken ct);
    Task<UserRole?> GetRoleAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<UserRole>> GetRolesByIdsAsync(int[] ids, CancellationToken ct);
    Task<bool> UserNameExistsAsync(string userName, long? exceptId, CancellationToken ct);
    Task<bool> RoleCodeExistsAsync(string code, CancellationToken ct);
    Task<ApplicationUser?> GetByNameAsync(string name, CancellationToken ct);
    Task<bool> HasOtherActiveAdminAsync(int exceptId, CancellationToken ct);
    Task RevokeTokensAsync(int userId, CancellationToken ct);
    Task<bool> WouldRemoveLastAdminForEmployeeAsync(int employeeId, CancellationToken ct);
    Task<bool> IsProtectedEmployeeAsync(long id, CancellationToken ct);
    void AddUser(ApplicationUser user);
    Task<UserRole?> GetRolesByCodeAsync(string code, CancellationToken ct);
    void AddRole(UserRole role);
    void RemoveMapping(UserRoleMapping mapping);
}
