using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace Kodiak.ContainerTracking.Tests;

public sealed class EmployeeDeletionTests
{
    [Fact]
    public async Task Delete_requires_version_preserves_profile_and_supports_reactivation()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        var created = await client.PostAsJsonAsync("/api/employees", TestData.Employee());
        var employee = await created.Content.ReadFromJsonAsync<JsonElement>();
        var path = "/api/employees/" + employee.GetProperty("uniqueId").GetInt32();
        Assert.Equal((HttpStatusCode)428, (await client.DeleteAsync(path)).StatusCode);
        using var deletion = new HttpRequestMessage(HttpMethod.Delete, path);
        deletion.Headers.IfMatch.Add(created.Headers.ETag!);
        var response = await client.SendAsync(deletion);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var terminated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, terminated.GetProperty("userStatus").GetInt32());
        Assert.Equal(employee.GetProperty("uniqueCode").GetString(), terminated.GetProperty("uniqueCode").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        using var stale = new HttpRequestMessage(HttpMethod.Delete, path);
        stale.Headers.IfMatch.Add(created.Headers.ETag!);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
        using var restore = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(TestData.Employee()) };
        restore.Headers.IfMatch.Add(response.Headers.ETag!);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(restore)).StatusCode);
    }

    [Fact]
    public async Task Delete_protects_final_admin_and_requires_authentication()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        var adminId = await factory.SeedAdminAsync();
        var admin = await client.GetFromJsonAsync<JsonElement>("/api/users/" + adminId);
        var path = "/api/employees/" + admin.GetProperty("linkedUniqueId").GetInt32();
        var employee = await client.GetAsync(path);
        using var deletion = new HttpRequestMessage(HttpMethod.Delete, path);
        deletion.Headers.IfMatch.Add(employee.Headers.ETag!);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(deletion)).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(path)).StatusCode);
    }
}
