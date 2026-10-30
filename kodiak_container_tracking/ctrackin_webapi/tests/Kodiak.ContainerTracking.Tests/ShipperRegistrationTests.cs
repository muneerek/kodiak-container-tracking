using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Kodiak.ContainerTracking.Tests;

public sealed class ShipperRegistrationTests
{
    private static ShipperRegistrationRequest Request(int consigneeId) => new("New Exporter", "Contact Person", "shipper@example.com", "+919876543210", "24 Port Road", consigneeId, "new.shipper", TestData.Password, TestData.Password);
    [Fact]
    public async Task Registration_atomically_creates_linked_shipper_and_hashed_user_then_can_login()
    {
        using var factory = new ApiFactory(useJwt: true); using var publicClient = factory.CreateApiClient(false);
        // Seed only the existing consignee; no administrative login or bootstrap is required for signup.
        int consigneeId;
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
            var consignee = new Consignee { Name = "Customer", EmailId = "private@example.com", Address = "Private address", PartyType = PartyType.Consignee, UniqueCode = "CON-EXISTING", Reference = new PartyReference { PartyType = PartyType.Consignee, UniqueCode = "CON-EXISTING" } };
            db.Consignees.Add(consignee); await db.SaveChangesAsync(); consigneeId = consignee.UniqueId;
        }
        var options = await publicClient.GetAsync("/api/registration/options/consignees?search=Customer");
        Assert.Equal(HttpStatusCode.OK, options.StatusCode); var json = await options.Content.ReadAsStringAsync(); Assert.DoesNotContain("private@example.com", json); Assert.DoesNotContain("Private address", json);
        var result = await publicClient.PostAsJsonAsync("/api/registration/shippers", Request(consigneeId));
        Assert.Equal(HttpStatusCode.Created, result.StatusCode); var registered = (await result.Content.ReadFromJsonAsync<ShipperRegistrationResponse>())!; Assert.Equal("SHP-000001", registered.UniqueCode);
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>(); var shipper = await db.Shippers.SingleAsync(); var user = await db.Users.SingleAsync();
            Assert.Equal(shipper.UniqueId, user.LinkedUniqueId); Assert.Equal(shipper.UniqueCode, user.LinkedUniqueCode); Assert.Equal(consigneeId, shipper.ConsigneeId); Assert.Equal(UserType.Shipper, user.UserType); Assert.NotEqual(TestData.Password, user.UserPassword);
            Assert.DoesNotContain(TestData.Password, string.Join("", await db.AccessAuditEvents.Select(x => x.Changes).ToListAsync()));
        }
        var loginResponse = await publicClient.PostAsJsonAsync("/api/auth/login", new { userName = registered.UserName, password = TestData.Password }); Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = (await loginResponse.Content.ReadFromJsonAsync<AuthenticationResponse>())!; publicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await publicClient.GetAsync("/api/users")).StatusCode);
    }
    [Theory]
    [InlineData("short")][InlineData("mismatch")][InlineData("consignee")][InlineData("country")][InlineData("reserved")]
    public async Task Invalid_registration_leaves_no_partial_records(string scenario)
    {
        using var factory = new ApiFactory(); using var admin = factory.CreateApiClient(); using var client = factory.CreateApiClient(false);
        var consigneeId = await UserManagementApiTests.CreateMaster(admin, 2); var input = Request(consigneeId);
        input = scenario switch {
            "short" => input with { Password = "short", ConfirmPassword = "short" },
            "mismatch" => input with { ConfirmPassword = "DifferentPassword42!" },
            "consignee" => input with { ConsigneeId = 999 },
            "country" => input with { CountryId = 999 },
            _ => input with { UserName = "superadmin" }
        };
        Assert.True((int)(await client.PostAsJsonAsync("/api/registration/shippers", input)).StatusCode is 400 or 409);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>(); Assert.Empty(db.Shippers); Assert.Empty(db.Users);
    }
    [Fact]
    public async Task Duplicate_and_privilege_injection_do_not_create_additional_records()
    {
        using var factory = new ApiFactory(); using var admin = factory.CreateApiClient(); using var client = factory.CreateApiClient(false);
        var consigneeId = await UserManagementApiTests.CreateMaster(admin, 2); var input = Request(consigneeId);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/registration/shippers", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/registration/shippers", input with { UserName = "NEW.SHIPPER" })).StatusCode);
        var injected = System.Text.Json.JsonSerializer.SerializeToNode(input)!.AsObject(); injected["userType"] = 1; injected["roleIds"] = new System.Text.Json.Nodes.JsonArray(1);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/registration/shippers", injected)).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>(); Assert.Single(db.Shippers); Assert.Single(db.Users);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Registration_without_consignee_accepts_null_or_omitted_value(bool omit)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient(false);
        var input = System.Text.Json.JsonSerializer.SerializeToNode(Request(1), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.AsObject();
        if (omit) input.Remove("consigneeId"); else input["consigneeId"] = null;
        var result = await client.PostAsJsonAsync("/api/registration/shippers", input);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        Assert.Null((await db.Shippers.SingleAsync()).ConsigneeId);
        Assert.Equal(UserType.Shipper, (await db.Users.SingleAsync()).UserType);
    }
    [Fact]
    public async Task Consignee_search_matches_partial_name_and_code_without_blacklisted_entries()
    {
        using var factory = new ApiFactory(); using var admin = factory.CreateApiClient(); using var client = factory.CreateApiClient(false);
        var first = await admin.PostAsJsonAsync("/api/consignees", TestData.Consignee("North Harbour Logistics"));
        var item = (await first.Content.ReadFromJsonAsync<PartyResponse>())!;
        await admin.PostAsJsonAsync("/api/consignees", TestData.Consignee("Another company"));
        await admin.PostAsJsonAsync("/api/consignees", TestData.Consignee("Harbour blocked") with { Status = BusinessStatus.Blacklisted });
        foreach (var term in new[] { "hArBoUr", item.UniqueCode.ToLowerInvariant() })
        {
            var result = (await client.GetFromJsonAsync<PagedResponse<RegistrationOption>>("/api/registration/options/consignees?search=" + Uri.EscapeDataString(term)))!;
            Assert.Single(result.Items); Assert.Equal(item.UniqueId, result.Items[0].Id);
        }
        Assert.Empty((await client.GetFromJsonAsync<PagedResponse<RegistrationOption>>("/api/registration/options/consignees?search=no-such-consignee"))!.Items);
    }
    [Fact]
    public async Task Repeated_public_registration_requests_are_rate_limited()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient(false);
        for (var attempt = 0; attempt < 5; attempt++) await client.PostAsJsonAsync("/api/registration/shippers", Request(999));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/registration/shippers", Request(999))).StatusCode);
    }
}
