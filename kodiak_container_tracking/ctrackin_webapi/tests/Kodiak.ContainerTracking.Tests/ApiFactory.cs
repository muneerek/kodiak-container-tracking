using System.Security.Claims;
using System.Text.Encodings.Web;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Kodiak.ContainerTracking.Tests;

public sealed class ApiFactory(bool useJwt = false, Dictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = TestData.SigningKey, ["Jwt:Issuer"] = "Kodiak.ContainerTracking", ["Jwt:Audience"] = "kodiak-container-tracking",
                ["Bootstrap:Enabled"] = "false", ["Bootstrap:InitialPassword"] = TestData.Password
            });
            if (settings != null) configuration.AddInMemoryCollection(settings);
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TrackingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TrackingDbContext>>();
            services.AddDbContext<TrackingDbContext>(options => options.UseSqlite(connection));
            if (useJwt) return;
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultForbidScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        });
    }

    public HttpClient CreateApiClient(bool authenticated = true)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        db.Database.EnsureCreated();
        // EnsureCreated does not execute the PostgreSQL catalog-seeding migration.
        if (!db.TimeZones.Any())
        {
            db.TimeZones.Add(new Kodiak.ContainerTracking.DataAccess.Entities.TimeZoneMaster
                { Name = "UTC", Abbreviation = "UTC", ObservedAt = DateTimeOffset.UtcNow });
            db.SaveChanges();
        }
        if (authenticated) client.DefaultRequestHeaders.Add("X-Test-Access", "admin");
        return client;
    }

    public async Task<int> SeedAdminAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Kodiak.ContainerTracking.Business.Services.IBootstrapService>().EnsureSuperAdminAsync(default);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}

// Test-only authentication. It is never registered by the application.
internal sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var access = Request.Headers["X-Test-Access"].ToString();
        if (access.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new("sub", "integration-test-admin"), new("user_type", "1") };
        if (access == "admin" || access == "ordinary-admin")
        {
            claims.Add(new Claim("permission", "users.manage"));
            claims.Add(new Claim("permission", "access.manage"));
        }
        if (access == "admin") claims.Add(new Claim("role", "SUPERADMIN"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, "role"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
