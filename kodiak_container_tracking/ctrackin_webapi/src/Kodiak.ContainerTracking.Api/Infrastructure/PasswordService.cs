using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
namespace Kodiak.ContainerTracking.Api.Infrastructure;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<ApplicationUser> hasher = new();
    private readonly ApplicationUser dummyUser = new();
    public PasswordService() => dummyUser.UserPassword = hasher.HashPassword(dummyUser, Guid.NewGuid().ToString("N"));
    public string Hash(ApplicationUser user, string password) => hasher.HashPassword(user, password);
    public bool Verify(ApplicationUser? user, string password)
    {
        var target = string.IsNullOrWhiteSpace(user?.UserPassword) ? dummyUser : user;
        try
        {
            var verified = hasher.VerifyHashedPassword(target, target.UserPassword!, password) != PasswordVerificationResult.Failed;
            return user != null && target != dummyUser && verified;
        }
        catch (FormatException) { return false; }
    }
}
