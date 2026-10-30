using System.Net;
using System.Net.Http.Json;
using Kodiak.ContainerTracking.Business.Contracts;
namespace Kodiak.ContainerTracking.Tests;
public sealed class CompanyDirectorySearchTests
{
    [Theory]
    [InlineData("consignees")]
    [InlineData("shippers")]
    public async Task Search_filters_before_pagination_and_matches_code_email_and_address(string kind)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        PartyResponse? target = null;
        foreach (var name in new[] { "Alpha", "Bravo", "Zulu Match" })
        {
            object body = kind == "consignees" ? TestData.Consignee(name) : TestData.Shipper(1, name) with { ConsigneeId = null };
            var response = await client.PostAsJsonAsync("/api/" + kind, body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            target = await response.Content.ReadFromJsonAsync<PartyResponse>();
        }
        Assert.NotNull(target);
        foreach (var term in new[] { "  ZULU  ", target.UniqueCode.ToLowerInvariant() })
        {
            var result = await client.GetFromJsonAsync<PagedResponse<PartyResponse>>("/api/" + kind + "?pageSize=1&search=" + Uri.EscapeDataString(term));
            Assert.NotNull(result); Assert.Equal(1, result.TotalCount);
            Assert.Equal(target.UniqueId, Assert.Single(result.Items).UniqueId);
        }
        foreach (var term in new[] { "@example.com", "road" })
        {
            var result = await client.GetFromJsonAsync<PagedResponse<PartyResponse>>("/api/" + kind + "?pageSize=1&search=" + Uri.EscapeDataString(term));
            Assert.NotNull(result); Assert.Equal(3, result.TotalCount); Assert.Single(result.Items);
        }
        var empty = await client.GetFromJsonAsync<PagedResponse<PartyResponse>>("/api/" + kind + "?search=no-such-company");
        Assert.NotNull(empty); Assert.Empty(empty.Items); Assert.Equal(0, empty.TotalCount);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/" + kind + "?search=zulu")).StatusCode);
    }
}
