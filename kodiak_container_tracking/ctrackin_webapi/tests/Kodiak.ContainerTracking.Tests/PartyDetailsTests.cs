using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace Kodiak.ContainerTracking.Tests;
public sealed class PartyDetailsTests
{
    [Fact]
    public async Task Country_state_and_company_relationships_are_validated()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        var country = await Create(client, "Countries", new { code = "IN", name = "India" });
        var state = await Create(client, "States", new { code = "KA", name = "Karnataka", countryId = country });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/masters/Companies", new { code = "WRONG", name = "Wrong", stateId = state })).StatusCode);
        await Create(client, "Companies", new { code = "KODIAK", name = "Kodiak", countryId = country, stateId = state });
    }
    [Fact]
    public async Task Organization_parent_company_and_cycles_are_validated()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        var company = await Create(client, "Companies", new { code = "ONE", name = "One" });
        var other = await Create(client, "Companies", new { code = "TWO", name = "Two" });
        var parent = await Create(client, "Organizations", new { code = "ROOT", name = "Head Office", companyId = company });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/masters/Organizations", new { code = "WRONG", name = "Wrong", companyId = other, parentOrgId = parent })).StatusCode);
        var child = await Create(client, "Organizations", new { code = "CHILD", name = "Branch", companyId = company, parentOrgId = parent });
        using var update = new HttpRequestMessage(HttpMethod.Put, $"/api/masters/Organizations/{parent}") { Content = JsonContent.Create(new { code = "ROOT", name = "Root", companyId = company, parentOrgId = child }) };
        update.Headers.Add("If-Match", "\"1\""); Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(update)).StatusCode);
    }
    private static async Task<int> Create(HttpClient client, string kind, object payload)
    { var response = await client.PostAsJsonAsync($"/api/masters/{kind}", payload); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32(); }
}
