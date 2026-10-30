using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public sealed class AuthenticationRepository(TrackingDbContext db) : IAuthenticationRepository
{
    public void AddLogin(UserLoginDetails login) => db.LoginDetails.Add(login);
    public void AddToken(UserToken token) => db.UserTokens.Add(token);
    public Task<UserToken?> GetTokenAsync(string hash, CancellationToken ct) => db.UserTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    public async Task CloseLoginsAsync(int userId, CancellationToken ct)
    {
        var logins = await db.LoginDetails.Where(x => x.FkAppUserId == userId && x.LogoutTime == null).ToListAsync(ct);
        foreach (var login in logins) login.LogoutTime = DateTimeOffset.UtcNow;
    }
    public async Task<(IReadOnlyList<UserLoginDetails> Items, int Total)> ListLoginsAsync(int userId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.LoginDetails.AsNoTracking().Where(x => x.FkAppUserId == userId);
        return (await query.OrderByDescending(x => x.LoginId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), await query.CountAsync(ct));
    }
    public async Task<(IReadOnlyList<UserToken> Items, int Total)> ListTokensAsync(int userId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.UserTokens.AsNoTracking().Where(x => x.FkAppUserId == userId);
        return (await query.OrderByDescending(x => x.TokenId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), await query.CountAsync(ct));
    }
}
