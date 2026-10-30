using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public interface IAuthenticationRepository
{
    void AddLogin(UserLoginDetails login);
    void AddToken(UserToken token);
    Task<UserToken?> GetTokenAsync(string hash, CancellationToken ct);
    Task CloseLoginsAsync(int userId, CancellationToken ct);
    Task<(IReadOnlyList<UserLoginDetails> Items, int Total)> ListLoginsAsync(int userId, int page, int pageSize, CancellationToken ct);
    Task<(IReadOnlyList<UserToken> Items, int Total)> ListTokensAsync(int userId, int page, int pageSize, CancellationToken ct);
}
