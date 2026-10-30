using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public sealed class UserManagementRepository(TrackingDbContext db) : IUserManagementRepository
{
    public async Task<(IReadOnlyList<ApplicationUser> Items, int Total)> GetUsersAsync(int page, int pageSize, string? search, string? uniqueCode, CancellationToken ct, bool includeSuperAdmin = true)
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!includeSuperAdmin) query = query.Where(x => !x.RoleMappings.Any(m => m.Role.NormalizedCode == "SUPERADMIN") && (x.Reference.Employee == null || x.Reference.Employee.Designation == null || x.Reference.Employee.Designation.Code != "SUPERADMIN"));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.NormalizedUserName.Contains(term) || x.LinkedUniqueCode.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(uniqueCode)) query = query.Where(x => x.LinkedUniqueCode == uniqueCode.Trim().ToUpperInvariant());
        var total = await query.CountAsync(ct);
        var items = await WithDetails(query)
            .OrderByDescending(x => x.AppUserId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<ApplicationUser?> GetUserAsync(long id, CancellationToken ct) =>
        WithDetails(db.Users).SingleOrDefaultAsync(x => x.AppUserId == id, ct);

    public async Task<(IReadOnlyList<UserRole> Items, int Total)> GetRolesAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.Roles.AsNoTracking();
        var total = await query.CountAsync(ct);
        return (await query.OrderBy(x => x.RoleName).ThenBy(x => x.RoleId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), total);
    }

    public Task<UserRole?> GetRoleAsync(int id, CancellationToken ct) => db.Roles.SingleOrDefaultAsync(x => x.RoleId == id, ct);
    public async Task<IReadOnlyList<UserRole>> GetRolesByIdsAsync(int[] ids, CancellationToken ct) => await db.Roles.Where(x => ids.Contains(x.RoleId)).ToListAsync(ct);
    public Task<bool> UserNameExistsAsync(string userName, long? exceptId, CancellationToken ct) =>
        db.Users.AnyAsync(x => x.AppUserId != exceptId && x.NormalizedUserName == userName, ct);
    public Task<bool> RoleCodeExistsAsync(string code, CancellationToken ct) => db.Roles.AnyAsync(x => x.NormalizedCode == code, ct);
    public Task<ApplicationUser?> GetByNameAsync(string name, CancellationToken ct) => WithDetails(db.Users).SingleOrDefaultAsync(x => x.NormalizedUserName == name, ct);
    public async Task<bool> HasOtherActiveAdminAsync(int exceptId, CancellationToken ct) => await db.Users.AnyAsync(x => x.AppUserId != exceptId && x.IsActive && x.UserType == UserType.Admin && x.UserPassword != null && x.Reference.Employee != null && x.Reference.Employee.UserStatus == EmployeeStatus.Active, ct);
    public async Task RevokeTokensAsync(int userId, CancellationToken ct)
    {
        var tokens = await db.UserTokens.Where(x => x.FkAppUserId == userId && x.RevokedOn == null).ToListAsync(ct);
        foreach (var token in tokens) token.RevokedOn = DateTimeOffset.UtcNow;
    }
    public async Task<bool> WouldRemoveLastAdminForEmployeeAsync(int employeeId, CancellationToken ct)
    {
        var query = db.Users.Where(x => x.IsActive && x.UserType == UserType.Admin && x.UserPassword != null && x.Reference.Employee != null && x.Reference.Employee.UserStatus == EmployeeStatus.Active);
        return await query.AnyAsync(x => x.LinkedUniqueId == employeeId, ct) && !await query.AnyAsync(x => x.LinkedUniqueId != employeeId, ct);
    }
    public Task<bool> IsProtectedEmployeeAsync(long id, CancellationToken ct) => db.Employees.AnyAsync(e => e.UniqueId == id &&
        ((e.Designation != null && e.Designation.Code == "SUPERADMIN") || db.Users.Any(u => u.LinkedUniqueId == id && u.RoleMappings.Any(m => m.Role.NormalizedCode == "SUPERADMIN"))), ct);
    public void AddUser(ApplicationUser user) => db.Users.Add(user);
    public Task<UserRole?> GetRolesByCodeAsync(string code, CancellationToken ct) => db.Roles.SingleOrDefaultAsync(x => x.NormalizedCode == code, ct);
    public void AddRole(UserRole role) => db.Roles.Add(role);
    public void RemoveMapping(UserRoleMapping mapping) => db.UserRoleMappings.Remove(mapping);

    internal static IQueryable<ApplicationUser> WithDetails(IQueryable<ApplicationUser> query) => query
        .Include(x => x.Reference).ThenInclude(x => x.Employee)
        .Include(x => x.Reference).ThenInclude(x => x.Consignee)
        .Include(x => x.Reference).ThenInclude(x => x.Shipper).ThenInclude(x => x!.Consignee)
        .Include(x => x.RoleMappings).ThenInclude(x => x.Role);
}
