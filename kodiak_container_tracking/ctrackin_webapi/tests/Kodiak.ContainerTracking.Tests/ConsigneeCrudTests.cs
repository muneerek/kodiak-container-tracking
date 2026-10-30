using System.Net;
using System.Net.Http.Json;
using Kodiak.ContainerTracking.Business.Contracts;
namespace Kodiak.ContainerTracking.Tests;

public sealed class ConsigneeCrudTests
{
    [Fact]
    public async Task Consignee_crud_preserves_linked_shippers_and_allows_reactivation()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        var created = await client.PostAsJsonAsync("/api/consignees", TestData.Consignee()); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var consignee = (await created.Content.ReadFromJsonAsync<PartyResponse>())!; var route = "/api/consignees/" + consignee.UniqueId;
        Assert.StartsWith("CON-", consignee.UniqueCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/consignees", TestData.Consignee() with { CountryId = 999 })).StatusCode);
        var shipperResponse = await client.PostAsJsonAsync("/api/shippers", TestData.Shipper(consignee.UniqueId)); var shipper = (await shipperResponse.Content.ReadFromJsonAsync<PartyResponse>())!;
        Assert.Equal((HttpStatusCode)428, (await client.DeleteAsync(route)).StatusCode);
        using var deletion = new HttpRequestMessage(HttpMethod.Delete, route); deletion.Headers.IfMatch.Add(created.Headers.ETag!);
        var deleted = await client.SendAsync(deletion); Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(2, (int)(await deleted.Content.ReadFromJsonAsync<PartyResponse>())!.Status!);
        Assert.Equal(consignee.UniqueId, (await client.GetFromJsonAsync<PartyResponse>("/api/shippers/" + shipper.UniqueId))!.ConsigneeId);
        Assert.Empty((await client.GetFromJsonAsync<PagedResponse<RegistrationOption>>("/api/registration/options/consignees"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/shippers", TestData.Shipper(consignee.UniqueId))).StatusCode);
        using var stale = new HttpRequestMessage(HttpMethod.Delete, route); stale.Headers.IfMatch.Add(created.Headers.ETag!); Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
        using var restore = new HttpRequestMessage(HttpMethod.Put, route) { Content = JsonContent.Create(TestData.Consignee("Restored customer")) }; restore.Headers.IfMatch.Add(deleted.Headers.ETag!);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(restore)).StatusCode);
        Assert.Equal("Restored customer", (await client.GetFromJsonAsync<PartyResponse>(route))!.CompanyName);
        Assert.Single((await client.GetFromJsonAsync<PagedResponse<RegistrationOption>>("/api/registration/options/consignees"))!.Items);
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(route)).StatusCode);
        using var restricted = factory.CreateApiClient(); restricted.DefaultRequestHeaders.Remove("X-Test-Access"); restricted.DefaultRequestHeaders.Add("X-Test-Access", "restricted"); Assert.Equal(HttpStatusCode.Forbidden, (await restricted.DeleteAsync(route)).StatusCode);
    }
}
