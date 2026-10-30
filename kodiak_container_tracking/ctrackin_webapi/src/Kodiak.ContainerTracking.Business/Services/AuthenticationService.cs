using System.Security.Cryptography;
using System.Text;
using Kodiak.ContainerTracking.Business.Configuration;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace Kodiak.ContainerTracking.Business.Services;

public interface IAccessTokenIssuer
{
    AccessTokenResult Issue(ApplicationUser user);
}
public interface IAuthenticationService
{
    Task<AuthenticationResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthenticationResponse> RefreshAsync(RefreshRequest request, CancellationToken ct);
    Task LogoutAsync(int userId, CancellationToken ct);
    Task<ApplicationUser?> ValidateAsync(int userId, long version, CancellationToken ct);
}

public sealed class AuthenticationService(IUserManagementRepository users, IAuthenticationRepository authentication,
    IUnitOfWork unitOfWork, IPasswordService passwords, IAccessTokenIssuer issuer, IOptions<JwtOptions> options) : IAuthenticationService
{
    public Task<AuthenticationResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request);
        return unitOfWork.InTransactionAsync<AuthenticationResponse>(async () =>
        {
            var user = await users.GetByNameAsync(request.UserName.Trim().ToUpperInvariant(), ct);
            var validPassword = passwords.Verify(user, request.Password);
            if (user == null || !validPassword || !IsEligible(user)) throw Unauthorized();
            authentication.AddLogin(new UserLoginDetails { User = user, FkAppUserId = user.AppUserId, LoginTime = DateTimeOffset.UtcNow });
            var refresh = AddRefresh(user);
            await unitOfWork.SaveAsync(user.AppUserId, user.AppUserId.ToString(), "Sign in", ct);
            var access = issuer.Issue(user);
            return new(access.AccessToken, refresh, "Bearer", access.ExpiresOn, user.ToResponse());
        }, ct);
    }
    public async Task<AuthenticationResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request);
        try
        {
            return await unitOfWork.InTransactionAsync(async () =>
            {
                var token = await authentication.GetTokenAsync(HashToken(request.RefreshToken), ct);
                if (token == null || token.Purpose != "REFRESH" || token.UsedOn != null || token.RevokedOn != null || token.ExpiresOn <= DateTimeOffset.UtcNow)
                    throw Unauthorized();
                var user = await users.GetUserAsync(token.FkAppUserId, ct);
                if (user == null || !IsEligible(user)) throw Unauthorized();
                token.UsedOn = DateTimeOffset.UtcNow;
                var refresh = AddRefresh(user);
                await unitOfWork.SaveAsync(user.AppUserId, user.AppUserId.ToString(), "Rotate refresh token", ct);
                var access = issuer.Issue(user);
                return new AuthenticationResponse(access.AccessToken, refresh, "Bearer", access.ExpiresOn, user.ToResponse());
            }, ct);
        }
        catch (DbUpdateConcurrencyException) { throw Unauthorized(); }
    }
    public Task LogoutAsync(int userId, CancellationToken ct) => unitOfWork.InTransactionAsync(async () =>
    {
        var user = await users.GetUserAsync(userId, ct) ?? throw Unauthorized();
        await users.RevokeTokensAsync(userId, ct); await authentication.CloseLoginsAsync(userId, ct);
        user.Version++;
        await unitOfWork.SaveAsync(userId, userId.ToString(), "Sign out all sessions", ct);
        return true;
    }, ct);
    public async Task<ApplicationUser?> ValidateAsync(int userId, long version, CancellationToken ct)
    {
        var user = await users.GetUserAsync(userId, ct);
        return user != null && user.Version == version && IsEligible(user) ? user : null;
    }
    public static bool IsEligible(ApplicationUser user) => user.IsActive && !string.IsNullOrEmpty(user.UserPassword) && (user.UserType switch
    {
        UserType.Normal or UserType.Admin => user.Reference.Employee is { UserStatus: EmployeeStatus.Active },
        UserType.Consignee => user.Reference.Consignee is { Status: BusinessStatus.Active },
        UserType.Shipper => user.Reference.Shipper is { Status: BusinessStatus.Active },
        _ => false
    });
    private string AddRefresh(ApplicationUser user)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        authentication.AddToken(new UserToken { User = user, FkAppUserId = user.AppUserId, Purpose = "REFRESH",
            TokenHash = HashToken(raw), ExpiresOn = DateTimeOffset.UtcNow.AddDays(options.Value.RefreshTokenDays) });
        return raw;
    }
    private static string HashToken(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    private static BusinessException Unauthorized() => new(401, "invalid_credentials", "Credentials or token are invalid, expired or unavailable.");
}
