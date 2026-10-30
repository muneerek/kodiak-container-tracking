using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.DataAccess.Entities;

namespace Kodiak.ContainerTracking.Business.Services;

public interface IRoleService
{
    Task<PagedResponse<RoleResponse>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<RoleResponse> GetAsync(int id, CancellationToken ct);
    Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken ct);
    Task<RoleResponse> UpdateAsync(int id, long version, UpdateRoleRequest request, CancellationToken ct);
}
