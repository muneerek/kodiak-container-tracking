using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
namespace Kodiak.ContainerTracking.Business.Services;
public sealed class UserService(IUserManagementRepository repository, IPartyRepository parties, IUnitOfWork unitOfWork,
    UserRoleAssignmentService assignments, ICurrentActor actor, IPasswordService passwords, TechnicalAccountAccess access) : IUserService
{
    public async Task<PagedResponse<UserResponse>> ListAsync(int page, int pageSize, string? search, string? uniqueCode, CancellationToken ct)
    {
        ServiceValidation.Page(page, pageSize); var (items, total) = await repository.GetUsersAsync(page, pageSize, search, uniqueCode, ct, actor.IsSuperAdmin);
        return new(items.Select(x => x.ToResponse()).ToArray(), total, page, pageSize);
    }
    public async Task<UserResponse> GetAsync(long id, CancellationToken ct) => (await Find(id, ct)).ToResponse();
    public Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request);
        return unitOfWork.InTransactionAsync(async () =>
        {
            var name = ServiceValidation.Required(request.UserName, "Username"); await CheckName(name, null, ct);
            await access.RequireEmployeeAsync(request.LinkedUniqueId, ct);
            var reference = await parties.GetReferenceAsync(request.LinkedUniqueId, ct);
            var expected = request.UserType is UserType.Normal or UserType.Admin ? PartyType.Employee : (PartyType)(int)request.UserType;
            if (reference?.PartyType != expected || reference.GetParty() == null) throw new BusinessException(400, "invalid_user_reference", "Select a linked record matching the account type.");
            var user = new ApplicationUser { UserName = name, NormalizedUserName = name.ToUpperInvariant(), UserType = request.UserType,
                LinkedUniqueId = reference.UniqueId, LinkedUniqueCode = reference.UniqueCode, Reference = reference, IsActive = request.IsActive };
            user.UserPassword = passwords.Hash(user, request.Password);
            await assignments.ApplyAsync(user, request.RoleIds, ct); repository.AddUser(user);
            await unitOfWork.SaveAsync(actor.UserId, actor.Subject, "Create application user", ct); return user.ToResponse();
        }, ct);
    }
    public Task<UserResponse> UpdateAsync(long id, long version, UpdateUserRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request);
        return Modify(id, version, async user =>
        {
            if (!request.IsActive && user.IsActive && user.UserType == UserType.Admin && !await repository.HasOtherActiveAdminAsync(user.AppUserId, ct))
                throw new BusinessException(409, "last_admin", "The last active administrator cannot be deactivated.");
            var name = ServiceValidation.Required(request.UserName, "Username"); await CheckName(name, user.AppUserId, ct);
            if (user.RoleMappings.Any(x => x.Role.Code == "SUPERADMIN") && !string.Equals(user.UserName, name, StringComparison.OrdinalIgnoreCase))
                throw new BusinessException(409, "protected_username", "The bootstrap super-admin username cannot be renamed.");
            user.UserName = name; user.NormalizedUserName = name.ToUpperInvariant(); user.IsActive = request.IsActive;
        }, "Update application user", ct);
    }
    public Task<UserResponse> ReplaceRolesAsync(long id, long version, ReplaceUserRolesRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request); ServiceValidation.Required(request.Reason, "Reason");
        return Modify(id, version, user => assignments.ApplyAsync(user, request.RoleIds, ct), request.Reason, ct);
    }
    public Task<UserResponse> SetPasswordAsync(long id, long version, SetPasswordRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request); ServiceValidation.Required(request.Reason, "Reason");
        return Modify(id, version, user => { user.UserPassword = passwords.Hash(user, request.Password); return Task.CompletedTask; }, request.Reason, ct);
    }
    private Task<UserResponse> Modify(long id, long version, Func<ApplicationUser, Task> modify, string reason, CancellationToken ct) => unitOfWork.InTransactionAsync(async () =>
    {
        var user = await Find(id, ct); ServiceValidation.Version(version, user.Version); await modify(user); user.Version++;
        await repository.RevokeTokensAsync(user.AppUserId, ct); await unitOfWork.SaveAsync(actor.UserId, actor.Subject, reason, ct); return user.ToResponse();
    }, ct);
    private async Task<ApplicationUser> Find(long id, CancellationToken ct)
    {
        var user = await repository.GetUserAsync(id, ct) ?? throw new BusinessException(404, "user_not_found", "Application user was not found.");
        await access.RequireUserAsync(user, ct); return user;
    }
    private async Task CheckName(string name, long? exceptId, CancellationToken ct)
    { if (await repository.UserNameExistsAsync(name.ToUpperInvariant(), exceptId, ct)) throw new BusinessException(409, "duplicate_username", "Username is already in use."); }
}
