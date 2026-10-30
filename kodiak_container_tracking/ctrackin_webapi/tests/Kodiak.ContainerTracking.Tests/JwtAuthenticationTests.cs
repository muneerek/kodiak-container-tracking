using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
namespace Kodiak.ContainerTracking.Tests;
public sealed class JwtAuthenticationTests
{
    [Fact]
    public async Task Bootstrap_is_idempotent_and_sign_in_stores_only_hashes_and_login_history()
    {
        using var factory = new ApiFactory(useJwt: true); using var client = factory.CreateApiClient(false);
        var id = await factory.SeedAdminAsync(); Assert.Equal(id, await factory.SeedAdminAsync());
        var login = await Login(client); Assert.Equal("superadmin", login.User.UserName); Assert.Equal(UserType.Admin, login.User.UserType);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        Assert.Equal(1, await db.Users.CountAsync()); Assert.Equal(1, await db.Employees.CountAsync());
        Assert.Equal("Super", (await db.Employees.SingleAsync()).FirstName); Assert.Equal("Admin", (await db.Employees.SingleAsync()).LastName);
        Assert.Equal(1, await db.Organizations.CountAsync()); Assert.Equal(1, await db.UserRoleMappings.CountAsync());
        Assert.NotEqual(TestData.Password, (await db.Users.SingleAsync()).UserPassword);
        Assert.Equal(1, await db.LoginDetails.CountAsync()); var token = await db.UserTokens.SingleAsync();
        Assert.Equal(64, token.TokenHash.Length); Assert.NotEqual(login.RefreshToken, token.TokenHash);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
    }
    [Fact]
    public async Task Refresh_is_single_use_and_logout_revokes_access_and_refresh_tokens()
    {
        using var factory = new ApiFactory(useJwt: true); using var client = factory.CreateApiClient(false); await factory.SeedAdminAsync();
        var login = await Login(client);
        var rotatedResponse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode); var rotated = (await rotatedResponse.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
        Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rotated.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = rotated.RefreshToken })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        Assert.All(await db.LoginDetails.ToListAsync(), x => Assert.NotNull(x.LogoutTime));
        Assert.All(await db.UserTokens.ToListAsync(), x => Assert.NotNull(x.RevokedOn));
    }
    [Theory] [InlineData("user")] [InlineData("employee")]
    public async Task Inactive_or_terminated_accounts_are_denied_on_next_request(string state)
    {
        using var factory = new ApiFactory(useJwt: true); using var client = factory.CreateApiClient(false); await factory.SeedAdminAsync();
        var login = await Login(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
            if (state == "user") (await db.Users.SingleAsync()).IsActive = false; else (await db.Employees.SingleAsync()).UserStatus = EmployeeStatus.Terminated;
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { userName = "superadmin", password = TestData.Password })).StatusCode);
    }
    [Theory] [InlineData("wrong-password")] [InlineData("unknown")]
    public async Task Invalid_credentials_return_generic_unauthorized(string scenario)
    {
        using var factory = new ApiFactory(useJwt: true); using var client = factory.CreateApiClient(false); await factory.SeedAdminAsync();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = scenario == "unknown" ? "unknown" : "superadmin", password = "Incorrect password" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Theory] [InlineData("valid", HttpStatusCode.OK)] [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("audience", HttpStatusCode.Unauthorized)] [InlineData("signature", HttpStatusCode.Unauthorized)]
    public async Task Bearer_validation_checks_signature_lifetime_and_audience(string scenario, HttpStatusCode expected)
    {
        using var factory = new ApiFactory(useJwt: true); using var client = factory.CreateApiClient(false); var id = await factory.SeedAdminAsync();
        var key = scenario == "signature" ? "different-test-only-signing-key-with-32-bytes-or-more" : TestData.SigningKey;
        var token = new JwtSecurityToken("Kodiak.ContainerTracking", scenario == "audience" ? "wrong" : "kodiak-container-tracking",
            [new Claim("sub", id.ToString()), new Claim("ver", "1")], DateTime.UtcNow.AddHours(-1),
            scenario == "expired" ? DateTime.UtcNow.AddMinutes(-2) : DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(expected, (await client.GetAsync("/api/users")).StatusCode);
    }
    [Fact]
    public async Task Normal_user_cannot_gain_admin_access_from_forged_type_claim()
    {
        using var factory = new ApiFactory(useJwt: true); using var client = factory.CreateApiClient(false); await factory.SeedAdminAsync();
        var admin = await Login(client); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        var master = await UserManagementApiTests.CreateMaster(client, 0); var role = await UserManagementApiTests.CreateRole(client, "VIEWER");
        var created = await client.PostAsJsonAsync("/api/users", new { userName = "normal", password = TestData.Password, userType = 0, linkedUniqueId = master, roleIds = new[] { role } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("appUserId").GetInt32();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "normal", password = TestData.Password });
        var login = (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        var forged = new JwtSecurityToken("Kodiak.ContainerTracking", "kodiak-container-tracking",
            [new Claim("sub", id.ToString()), new Claim("ver", "1"), new Claim("user_type", "1"), new Claim("permission", "users.manage")],
            DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestData.SigningKey)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(forged));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
    }
    [Fact]
    public async Task Session_metadata_hides_token_hash_and_password_reset_revokes_old_tokens()
    {
        using var factory = new ApiFactory(useJwt: true); using var client = factory.CreateApiClient(false); var id = await factory.SeedAdminAsync();
        var login = await Login(client); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var tokens = await client.GetAsync($"/api/users/{id}/tokens"); Assert.Equal(HttpStatusCode.OK, tokens.StatusCode);
        var metadata = await tokens.Content.ReadAsStringAsync(); Assert.DoesNotContain("tokenHash", metadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(login.RefreshToken, metadata);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/users/{id}/logins")).StatusCode);
        using var reset = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{id}/password") { Content = JsonContent.Create(new { password = "Another-test-password42!", reason = "Password reset test" }) };
        reset.Headers.Add("If-Match", "\"1\""); Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(reset)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { userName = "superadmin", password = TestData.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { userName = "superadmin", password = "Another-test-password42!" })).StatusCode);
    }
    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        using var factory = new ApiFactory(useJwt: true); using var client = factory.CreateApiClient(false); await factory.SeedAdminAsync(); var login = await Login(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>(); (await db.UserTokens.SingleAsync()).ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken })).StatusCode);
    }
    private static async Task<AuthenticationResponse> Login(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "superadmin", password = TestData.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
    }
}
