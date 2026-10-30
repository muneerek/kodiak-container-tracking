using System.Net;
using System.Net.Http.Json;
using Kodiak.ContainerTracking.Business.Contracts;
namespace Kodiak.ContainerTracking.Tests;

public sealed class BusinessCategoryTests
{
    [Fact]
    public async Task Category_crud_preserves_shipper_links_and_can_reactivate()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        var created = await client.PostAsJsonAsync("/api/masters/BusinessCategories", new { code = "EXPORT", name = "Export" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var category = (await created.Content.ReadFromJsonAsync<MasterResponse>())!;
        var route = "/api/masters/BusinessCategories/" + category.Id;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/masters/BusinessCategories", new { code = "export", name = "Duplicate" })).StatusCode);
        var consignee = await UserManagementApiTests.CreateMaster(client, 2);
        var shipper = await client.PostAsJsonAsync("/api/shippers", TestData.Shipper(consignee) with { BusinessCategoryId = category.Id }); Assert.Equal(HttpStatusCode.Created, shipper.StatusCode);
        var shipperId = (await shipper.Content.ReadFromJsonAsync<PartyResponse>())!.UniqueId;
        Assert.Equal((HttpStatusCode)428, (await client.DeleteAsync(route)).StatusCode);
        using var deletion = new HttpRequestMessage(HttpMethod.Delete, route); deletion.Headers.IfMatch.Add(created.Headers.ETag!);
        var deleted = await client.SendAsync(deletion); Assert.Equal(HttpStatusCode.OK, deleted.StatusCode); Assert.False((await deleted.Content.ReadFromJsonAsync<MasterResponse>())!.IsActive);
        var options = await client.GetFromJsonAsync<PagedResponse<RegistrationOption>>("/api/registration/options/businesscategories"); Assert.Empty(options!.Items);
        Assert.Equal(category.Id, (await client.GetFromJsonAsync<PartyResponse>("/api/shippers/" + shipperId))!.BusinessCategoryId);
        using var stale = new HttpRequestMessage(HttpMethod.Delete, route); stale.Headers.IfMatch.Add(created.Headers.ETag!); Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
        using var restore = new HttpRequestMessage(HttpMethod.Put, route) { Content = JsonContent.Create(new { code = "EXPORT", name = "Export and logistics", isActive = true }) }; restore.Headers.IfMatch.Add(deleted.Headers.ETag!);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(restore)).StatusCode);
        Assert.Equal("Export and logistics", (await client.GetFromJsonAsync<MasterResponse>(route))!.Name);
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(route)).StatusCode);
    }
}
