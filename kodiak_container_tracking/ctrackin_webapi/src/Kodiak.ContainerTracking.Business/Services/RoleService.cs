using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;

namespace Kodiak.ContainerTracking.Business.Services;

public sealed class RoleService(IUserManagementRepository repository, IUnitOfWork unitOfWork, ICurrentActor actor) : IRoleService
{
    public async Task<PagedResponse<RoleResponse>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        ServiceValidation.Page(page, pageSize);
        var (items, total) = await repository.GetRolesAsync(page, pageSize, ct);
        return new(items.Select(x => x.ToResponse()).ToArray(), total, page, pageSize);
    }

    public async Task<RoleResponse> GetAsync(int id, CancellationToken ct) => (await FindAsync(id, ct)).ToResponse();

    public Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request);
        return unitOfWork.InTransactionAsync(async () =>
        {
            var code = ServiceValidation.Required(request.Code, "Code").ToUpperInvariant();
            if (code == "SUPERADMIN") throw new BusinessException(409, "reserved_role", "SUPERADMIN is managed by controlled bootstrap.");
            if (await repository.RoleCodeExistsAsync(code, ct))
                throw new BusinessException(409, "duplicate_role_code", "Role code is already in use.");
            var role = new UserRole { Code = code, NormalizedCode = code,
                RoleName = ServiceValidation.Required(request.RoleName, "Role name"),
                Description = ServiceValidation.Optional(request.Description) };
            repository.AddRole(role);
            await unitOfWork.SaveAsync(actor.UserId, actor.Subject, "Create role", ct);
            return role.ToResponse();
        }, ct);
    }

    public Task<RoleResponse> UpdateAsync(int id, long version, UpdateRoleRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request);
        ServiceValidation.Required(request.Reason, "Reason");
        return unitOfWork.InTransactionAsync(async () =>
        {
            var role = await FindAsync(id, ct);
            ServiceValidation.Version(version, role.Version);
            if (role.Code == "SUPERADMIN" && !actor.IsSuperAdmin) throw new BusinessException(404, "role_not_found", "Role was not found.");
            if (role.Code == "SUPERADMIN" && !request.IsActive) throw new BusinessException(409, "protected_role", "The SUPERADMIN role cannot be deactivated.");
            role.RoleName = ServiceValidation.Required(request.RoleName, "Role name");
            role.Description = ServiceValidation.Optional(request.Description);
            role.IsActive = request.IsActive;
            role.Version++;
            await unitOfWork.SaveAsync(actor.UserId, actor.Subject, request.Reason, ct);
            return role.ToResponse();
        }, ct);
    }

    private async Task<UserRole> FindAsync(int id, CancellationToken ct) => await repository.GetRoleAsync(id, ct)
        ?? throw new BusinessException(404, "role_not_found", "Role was not found.");
}
