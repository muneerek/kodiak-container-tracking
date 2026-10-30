using Kodiak.ContainerTracking.Business.Contracts;

namespace Kodiak.ContainerTracking.Business.Services;

public interface IUserService
{
    Task<PagedResponse<UserResponse>> ListAsync(int page, int pageSize, string? search, string? uniqueCode, CancellationToken ct);
    Task<UserResponse> GetAsync(long id, CancellationToken ct);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserResponse> UpdateAsync(long id, long version, UpdateUserRequest request, CancellationToken ct);
    Task<UserResponse> SetPasswordAsync(long id, long version, SetPasswordRequest request, CancellationToken ct);
    Task<UserResponse> ReplaceRolesAsync(long id, long version, ReplaceUserRolesRequest request, CancellationToken ct);
}
