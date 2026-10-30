using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kodiak.ContainerTracking.Business.Configuration;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace Kodiak.ContainerTracking.Api.Infrastructure;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : IAccessTokenIssuer
{
    public AccessTokenResult Issue(ApplicationUser user)
    {
        var configuration = options.Value;
        var expires = DateTimeOffset.UtcNow.AddMinutes(configuration.AccessTokenMinutes);
        var token = new JwtSecurityToken(configuration.Issuer, configuration.Audience, Claims(user),
            DateTime.UtcNow, expires.UtcDateTime, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
    public static IEnumerable<Claim> Claims(ApplicationUser user)
    {
        yield return new("sub", user.AppUserId.ToString()); yield return new("name", user.UserName);
        yield return new("user_type", ((int)user.UserType).ToString()); yield return new("ver", user.Version.ToString());
        yield return new("local_user_id", user.AppUserId.ToString()); yield return new("jti", Guid.NewGuid().ToString("N"));
        foreach (var role in user.RoleMappings.Where(x => x.Role.IsActive)) yield return new("role", role.Role.Code);
        if (user.UserType == UserType.Admin)
        {
            yield return new("permission", "users.manage"); yield return new("permission", "access.manage");
        }
    }
}
