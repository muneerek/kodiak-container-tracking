using Kodiak.ContainerTracking.Business.Seeding;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Kodiak.ContainerTracking.Tests;

public sealed class GeographySeederTests
{
    [Fact]
    public async Task Seeds_all_countries_and_requested_subdivisions_once_with_valid_links()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient(); using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGeographySeeder>(); var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var first = await service.SeedAsync(true, default); Assert.Equal(249, first.CountriesAdded); Assert.Equal(77, first.StatesAdded);
        var second = await service.SeedAsync(true, default); Assert.Equal(new GeographySeedResult(0, 0), second);
        Assert.Equal(36, await db.Set<StateMaster>().CountAsync(x => x.Country.Code == "IN"));
        Assert.Equal(7, await db.Set<StateMaster>().CountAsync(x => x.Country.Code == "AE"));
        Assert.Equal(34, await db.Set<StateMaster>().CountAsync(x => x.Country.Code == "CN"));
        Assert.Equal("Dubai", (await db.Set<StateMaster>().SingleAsync(x => x.Code == "AE-DU")).Name);
        Assert.Equal("Kerala", (await db.Set<StateMaster>().SingleAsync(x => x.Code == "IN-KL")).Name);
        Assert.All(await db.Set<StateMaster>().ToListAsync(), x => Assert.True(x.CountryId > 0));
        Assert.Single(await db.AccessAuditEvents.ToListAsync());
    }
    [Fact]
    public async Task Preserves_legacy_codes_ids_custom_names_and_inactive_flags()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var india = new CountryMaster { Code = "IND", Name = "India", IsActive = false };
        var kerala = new StateMaster { Code = "KL", Name = "Kerala", IsActive = false, Country = india };
        db.Add(kerala); await db.SaveChangesAsync(); var id = india.Id; var stateId = kerala.Id;
        var result = await scope.ServiceProvider.GetRequiredService<IGeographySeeder>().SeedAsync(true, default);
        Assert.Equal(248, result.CountriesAdded); Assert.Equal(76, result.StatesAdded);
        Assert.Equal(id, india.Id); Assert.Equal(stateId, kerala.Id); Assert.Equal("IND", india.Code); Assert.Equal("KL", kerala.Code); Assert.False(india.IsActive); Assert.False(kerala.IsActive);
        Assert.Equal(36, await db.Set<StateMaster>().CountAsync(x => x.CountryId == id));
    }
    [Fact]
    public async Task Wrong_country_link_stops_seeding_without_partial_inserts()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
            db.Add(new StateMaster { Code = "IN-KL", Name = "Wrong link", Country = new CountryMaster { Code = "AE", Name = "United Arab Emirates" } });
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<BusinessException>(() => scope.ServiceProvider.GetRequiredService<IGeographySeeder>().SeedAsync(true, default));
        }
        using var verify = factory.Services.CreateScope(); var check = verify.ServiceProvider.GetRequiredService<TrackingDbContext>();
        Assert.Single(await check.Set<CountryMaster>().ToListAsync()); Assert.Single(await check.Set<StateMaster>().ToListAsync()); Assert.Empty(await check.AccessAuditEvents.ToListAsync());
    }
    [Fact]
    public async Task Countries_can_be_seeded_separately_before_states()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient(); using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGeographySeeder>();
        Assert.Equal(new GeographySeedResult(249, 0), await service.SeedAsync(false, default));
        Assert.Equal(new GeographySeedResult(0, 77), await service.SeedAsync(true, default));
    }
}
