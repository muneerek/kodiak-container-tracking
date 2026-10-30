using System.Net;
using System.Net.Http.Json;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Kodiak.ContainerTracking.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public async Task Missing_password_leaves_no_partial_bootstrap_records()
    {
        using var factory = new ApiFactory(settings: new() { ["Bootstrap:InitialPassword"] = "" });
        using var client = factory.CreateApiClient();
        var error = await Assert.ThrowsAsync<BusinessException>(() => factory.SeedAdminAsync());
        Assert.Equal("bootstrap_password_required", error.Code);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Employees.CountAsync());
        Assert.Equal(0, await db.Companies.CountAsync());
    }

    [Fact]
    public async Task Existing_normal_username_is_never_promoted_by_bootstrap()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        var employee = await UserManagementApiTests.CreateMaster(client, 0);
        var role = await UserManagementApiTests.CreateRole(client, "VIEWER");
        var response = await client.PostAsJsonAsync("/api/users", new { userName = "superadmin", password = TestData.Password, userType = 0, linkedUniqueId = employee, roleIds = new[] { role } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var error = await Assert.ThrowsAsync<BusinessException>(() => factory.SeedAdminAsync());
        Assert.Equal("bootstrap_conflict", error.Code);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        Assert.Equal(UserType.Normal, (await db.Users.SingleAsync()).UserType);
        Assert.Equal(1, await db.Employees.CountAsync());
    }
}
