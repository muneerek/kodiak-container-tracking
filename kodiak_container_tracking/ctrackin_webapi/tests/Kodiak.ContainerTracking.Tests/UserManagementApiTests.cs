using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Kodiak.ContainerTracking.Tests;
public sealed class UserManagementApiTests
{
    [Fact]
    public async Task Anonymous_and_unprivileged_callers_are_denied()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient(false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Access", "reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task Account_types_link_to_matching_master_and_hide_credentials(int type)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        var id = await CreateMaster(client, type); var role = await CreateRole(client, "OPERATIONS");
        var response = await client.PostAsJsonAsync("/api/users", new { userName = "operator", password = TestData.Password, userType = type, linkedUniqueId = id, roleIds = new[] { role } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("appUserId").GetInt32() > 0); Assert.Equal(id, body.GetProperty("linkedUniqueId").GetInt32());
        Assert.Equal(type, body.GetProperty("userType").GetInt32()); Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.False(body.TryGetProperty("userPassword", out _)); Assert.False(body.TryGetProperty("password", out _));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var user = await db.Users.SingleAsync(); Assert.NotEqual(TestData.Password, user.UserPassword);
        Assert.Equal(1, await db.UserRoleMappings.CountAsync());
        Assert.DoesNotContain(TestData.Password, string.Join(" ", await db.AccessAuditEvents.Select(x => x.Changes).ToListAsync()));
    }
    [Fact]
    public async Task Invalid_assignments_and_duplicate_names_leave_no_partial_user()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        var id = await CreateMaster(client, 0); var role = await CreateRole(client, "ROLE");
        foreach (var ids in new[] { new[] { role, role }, new[] { 999 } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", new { userName = "a", password = TestData.Password, userType = 0, linkedUniqueId = id, roleIds = ids })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users", new { userName = "a", password = TestData.Password, userType = 2, linkedUniqueId = id, roleIds = new[] { role } })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/users", new { userName = "a", password = TestData.Password, userType = 0, linkedUniqueId = id, roleIds = new[] { role } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/users", new { userName = "A", password = TestData.Password, userType = 0, linkedUniqueId = id, roleIds = new[] { role } })).StatusCode);
        using var scope = factory.Services.CreateScope(); Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<TrackingDbContext>().Users.CountAsync());
    }
    [Fact]
    public async Task Mapping_replacement_requires_current_etag()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        var id = await CreateMaster(client, 0); var role = await CreateRole(client, "ROLE");
        var response = await client.PostAsJsonAsync("/api/users", new { userName = "a", password = TestData.Password, userType = 0, linkedUniqueId = id, roleIds = new[] { role } });
        var appId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("appUserId").GetInt32(); var route = $"/api/users/{appId}/roles";
        Assert.Equal((HttpStatusCode)428, (await client.PutAsJsonAsync(route, new { roleIds = Array.Empty<int>(), reason = "Remove" })).StatusCode);
        using var update = new HttpRequestMessage(HttpMethod.Put, route) { Content = JsonContent.Create(new { roleIds = Array.Empty<int>(), reason = "Remove" }) };
        update.Headers.Add("If-Match", "\"1\""); var saved = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode); Assert.Equal("\"2\"", saved.Headers.ETag?.Tag);
        using var stale = new HttpRequestMessage(HttpMethod.Put, route) { Content = JsonContent.Create(new { roleIds = new[] { role }, reason = "Restore" }) };
        stale.Headers.Add("If-Match", "\"1\""); Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
    }
    [Fact]
    public async Task Last_admin_is_protected()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient(); var admin = await factory.SeedAdminAsync();
        using var update = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{admin}") { Content = JsonContent.Create(new { userName = "superadmin", isActive = false }) };
        update.Headers.Add("If-Match", "\"1\""); Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/roles", new { code = "SUPERADMIN", roleName = "Forged" })).StatusCode);
    }
    [Theory] [InlineData("/api/users?page=0")] [InlineData("/api/users?pageSize=101")]
    public async Task Invalid_pagination_is_rejected(string route)
    { using var factory = new ApiFactory(); using var client = factory.CreateApiClient(); Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(route)).StatusCode); }
    internal static async Task<int> CreateMaster(HttpClient client, int type)
    {
        string route; object payload;
        if (type is 0 or 1) { route = "employees"; payload = TestData.Employee(); }
        else if (type == 2) { route = "consignees"; payload = TestData.Consignee(); }
        else { var consignee = await CreateMaster(client, 2); route = "shippers"; payload = TestData.Shipper(consignee); }
        var result = await client.PostAsJsonAsync($"/api/{route}", payload); Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        return (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uniqueId").GetInt32();
    }
    internal static async Task<int> CreateRole(HttpClient client, string code)
    { var response = await client.PostAsJsonAsync("/api/roles", new { code, roleName = code }); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("roleId").GetInt32(); }
}
