using Kodiak.ContainerTracking.DataAccess.Repositories;
namespace Kodiak.ContainerTracking.Business.Seeding;

public sealed record GeographySeedResult(int CountriesAdded, int StatesAdded);
public interface IGeographySeeder { Task<GeographySeedResult> SeedAsync(bool includeStates, CancellationToken ct); }
public sealed class GeographySeeder(CountrySeeder countries, StateProvinceSeeder states, IUnitOfWork unitOfWork) : IGeographySeeder
{
    public Task<GeographySeedResult> SeedAsync(bool includeStates, CancellationToken ct) => unitOfWork.InTransactionAsync(async () =>
    {
        var (countryRecords, countriesAdded) = await countries.SeedAsync(ct);
        var statesAdded = includeStates ? await states.SeedAsync(countryRecords, ct) : 0;
        if (countriesAdded + statesAdded > 0) await unitOfWork.SaveAsync(null, "geography-seeder", "Seed countries and states/provinces", ct);
        return new GeographySeedResult(countriesAdded, statesAdded);
    }, ct);
}
