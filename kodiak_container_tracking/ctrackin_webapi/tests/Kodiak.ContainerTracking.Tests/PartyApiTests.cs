using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace Kodiak.ContainerTracking.Tests;
public sealed class PartyApiTests
{
    [Fact]
    public async Task Codes_keep_separate_configurable_sequences_and_lookup()
    {
        using var factory = new ApiFactory(settings: new() { ["UniqueCodes:EmployeePrefix"] = "STAFF-", ["UniqueCodes:Padding"] = "4" }); using var client = factory.CreateApiClient();
        await UserManagementApiTests.CreateMaster(client, 0); await UserManagementApiTests.CreateMaster(client, 2);
        Assert.Equal("Aisha Khan", (await client.GetFromJsonAsync<JsonElement>("/api/parties/by-code/staff-0001")).GetProperty("name").GetString());
        Assert.Equal("Customer", (await client.GetFromJsonAsync<JsonElement>("/api/parties/by-code/CON-0001")).GetProperty("companyName").GetString());
    }
    [Fact]
    public async Task Final_employee_details_validate_designation_links()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/employees", TestData.Employee() with { DesignationId = 999 })).StatusCode);
        var designationResponse = await client.PostAsJsonAsync("/api/masters/Designations", new { code = "OPS", name = "Operations" });
        var designation = (await designationResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var response = await client.PostAsJsonAsync("/api/employees", TestData.Employee() with { DesignationId = designation, Logo = "employee-logo.png" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var employee = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(designation, employee.GetProperty("designationId").GetInt32()); Assert.Equal("employee-logo.png", employee.GetProperty("logo").GetString());
        Assert.Equal(1, employee.GetProperty("userStatus").GetInt32()); Assert.False(employee.TryGetProperty("gender", out _));
    }
    [Fact]
    public async Task Shipper_requires_one_existing_active_consignee()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/shippers", TestData.Shipper(999))).StatusCode);
        var id = await UserManagementApiTests.CreateMaster(client, 2); var response = await client.PostAsJsonAsync("/api/shippers", TestData.Shipper(id));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var shipper = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, shipper.GetProperty("consigneeId").GetInt32()); Assert.Equal("Customer", shipper.GetProperty("consignee").GetProperty("companyName").GetString());
    }
}
