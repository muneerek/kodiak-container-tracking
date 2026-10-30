using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kodiak.ContainerTracking.Business.Contracts;
namespace Kodiak.ContainerTracking.Tests;

public sealed class TechnicalAccountTests
{
    [Fact]
    public async Task Ordinary_admin_cannot_discover_read_or_modify_technical_records()
    {
        using var factory = new ApiFactory(); using var super = factory.CreateApiClient();
        var id = await factory.SeedAdminAsync();
        var user = (await super.GetFromJsonAsync<UserResponse>("/api/users/" + id))!;
        var employee = user.Details;
        using var ordinary = factory.CreateApiClient(); ordinary.DefaultRequestHeaders.Remove("X-Test-Access"); ordinary.DefaultRequestHeaders.Add("X-Test-Access", "ordinary-admin");
        Assert.Equal(0, (await ordinary.GetFromJsonAsync<PagedResponse<UserResponse>>("/api/users"))!.TotalCount);
        Assert.Equal(0, (await ordinary.GetFromJsonAsync<PagedResponse<PartyResponse>>("/api/employees"))!.TotalCount);
        foreach (var route in new[] { $"/api/users/{id}", $"/api/users/{id}/roles", $"/api/users/{id}/logins", $"/api/users/{id}/tokens", $"/api/employees/{employee.UniqueId}", $"/api/employees/{employee.UniqueId}/photo", $"/api/parties/by-code/{employee.UniqueCode}" })
            Assert.Equal(HttpStatusCode.NotFound, (await ordinary.GetAsync(route)).StatusCode);
        foreach (var request in new[] {
            new HttpRequestMessage(HttpMethod.Put, $"/api/employees/{employee.UniqueId}") { Content = JsonContent.Create(TestData.Employee()) },
            new HttpRequestMessage(HttpMethod.Delete, $"/api/employees/{employee.UniqueId}"),
            new HttpRequestMessage(HttpMethod.Put, $"/api/users/{id}") { Content = JsonContent.Create(new { userName = "superadmin", isActive = true }) },
            new HttpRequestMessage(HttpMethod.Post, $"/api/users/{id}/password") { Content = JsonContent.Create(new { password = TestData.Password, reason = "attempt" }) },
            new HttpRequestMessage(HttpMethod.Post, $"/api/users/{id}/revoke-sessions") { Content = JsonContent.Create(new { reason = "attempt" }) }
        }) { using (request) { request.Headers.Add("If-Match", "\"1\""); Assert.Equal(HttpStatusCode.NotFound, (await ordinary.SendAsync(request)).StatusCode); } }
        using (var photo = new MultipartFormDataContent())
        {
            photo.Add(new ByteArrayContent(new byte[] { 255, 216, 255, 217 }), "photo", "photo.jpg");
            using var attempt = new HttpRequestMessage(HttpMethod.Put, $"/api/employees/{employee.UniqueId}/photo") { Content = photo };
            attempt.Headers.Add("If-Match", "\"1\"");
            Assert.Equal(HttpStatusCode.NotFound, (await ordinary.SendAsync(attempt)).StatusCode);
        }
        using (var attempt = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{id}/roles") { Content = JsonContent.Create(new { roleIds = new[] { user.Roles.Single().RoleId }, reason = "attempt" }) })
        {
            attempt.Headers.Add("If-Match", "\"1\"");
            Assert.Equal(HttpStatusCode.NotFound, (await ordinary.SendAsync(attempt)).StatusCode);
        }
        var designation = employee.DesignationId;
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/employees", TestData.Employee() with { DesignationId = designation })).StatusCode);
        var normal = await ordinary.PostAsJsonAsync("/api/employees", TestData.Employee());
        var normalId = (await normal.Content.ReadFromJsonAsync<PartyResponse>())!.UniqueId;
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/api/users", new { userName = "escalation", password = TestData.Password, userType = 1, linkedUniqueId = normalId, roleIds = new[] { user.Roles.Single().RoleId } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ordinary.PostAsJsonAsync("/api/users", new { userName = "alias", password = TestData.Password, userType = 1, linkedUniqueId = employee.UniqueId, roleIds = new[] { user.Roles.Single().RoleId } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await super.GetAsync($"/api/employees/{employee.UniqueId}")).StatusCode);
        Assert.Equal(2, (await super.GetFromJsonAsync<PagedResponse<PartyResponse>>("/api/employees"))!.TotalCount);
    }
}
