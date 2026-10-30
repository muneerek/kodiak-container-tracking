using System.ComponentModel.DataAnnotations;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Repositories;
namespace Kodiak.ContainerTracking.Business.Services;

public sealed record LoginHistoryResponse(int LoginId, int FkAppUserId, DateTimeOffset LoginTime, DateTimeOffset? LogoutTime);
public sealed record TokenMetadataResponse(int TokenId, string Purpose, DateTimeOffset ExpiresOn, DateTimeOffset? UsedOn, DateTimeOffset? RevokedOn, DateTimeOffset CreateOn);
public sealed record RevokeSessionsRequest([Required, StringLength(1000)] string Reason);
public interface IUserSessionsService
{
    Task<PagedResponse<LoginHistoryResponse>> LoginsAsync(int userId, int page, int pageSize, CancellationToken ct);
    Task<PagedResponse<TokenMetadataResponse>> TokensAsync(int userId, int page, int pageSize, CancellationToken ct);
    Task RevokeAsync(int userId, long version, RevokeSessionsRequest request, CancellationToken ct);
}
public sealed class UserSessionsService(IUserManagementRepository users, IAuthenticationRepository authentication, IUnitOfWork unitOfWork, ICurrentActor actor, TechnicalAccountAccess access) : IUserSessionsService
{
    public async Task<PagedResponse<LoginHistoryResponse>> LoginsAsync(int userId, int page, int pageSize, CancellationToken ct)
    {
        ServiceValidation.Page(page, pageSize); await RequireUser(userId, ct);
        var (items, total) = await authentication.ListLoginsAsync(userId, page, pageSize, ct);
        return new(items.Select(x => new LoginHistoryResponse(x.LoginId, x.FkAppUserId, x.LoginTime, x.LogoutTime)).ToArray(), total, page, pageSize);
    }
    public async Task<PagedResponse<TokenMetadataResponse>> TokensAsync(int userId, int page, int pageSize, CancellationToken ct)
    {
        ServiceValidation.Page(page, pageSize); await RequireUser(userId, ct);
        var (items, total) = await authentication.ListTokensAsync(userId, page, pageSize, ct);
        return new(items.Select(x => new TokenMetadataResponse(x.TokenId, x.Purpose, x.ExpiresOn, x.UsedOn, x.RevokedOn, x.CreateOn)).ToArray(), total, page, pageSize);
    }
    public Task RevokeAsync(int userId, long version, RevokeSessionsRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request); ServiceValidation.Required(request.Reason, "Reason");
        return unitOfWork.InTransactionAsync(async () =>
        {
            var user = await RequireUser(userId, ct); ServiceValidation.Version(version, user.Version);
            await users.RevokeTokensAsync(userId, ct); await authentication.CloseLoginsAsync(userId, ct); user.Version++;
            await unitOfWork.SaveAsync(actor.UserId, actor.Subject, request.Reason, ct); return true;
        }, ct);
    }
    private async Task<Kodiak.ContainerTracking.DataAccess.Entities.ApplicationUser> RequireUser(int id, CancellationToken ct)
    {
        var user = await users.GetUserAsync(id, ct) ?? throw new BusinessException(404, "user_not_found", "User was not found.");
        await access.RequireUserAsync(user, ct); return user;
    }
}
