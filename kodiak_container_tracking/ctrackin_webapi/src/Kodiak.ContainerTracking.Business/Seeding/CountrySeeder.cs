using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
namespace Kodiak.ContainerTracking.Business.Seeding;

public sealed class CountrySeeder(IGeographySeedRepository repository)
{
    internal async Task<(Dictionary<string, CountryMaster> Countries, int Added)> SeedAsync(CancellationToken ct)
    {
        var existing = await repository.CountriesAsync(ct);
        var countries = new Dictionary<string, CountryMaster>(StringComparer.OrdinalIgnoreCase); var added = 0;
        foreach (var seed in GeographySeedData.Countries)
        {
            var matches = existing.Where(x => string.Equals(x.Code, seed.Code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Code, seed.Alpha3, StringComparison.OrdinalIgnoreCase)
                || (seed.Code == "AE" && string.Equals(x.Code, "UAE", StringComparison.OrdinalIgnoreCase))
                || GeographySeedData.Normalize(x.Name) == GeographySeedData.Normalize(seed.Name)).ToArray();
            if (matches.Length > 1) throw new BusinessException(409, "country_seed_conflict", $"Multiple country records match {seed.Code}. Resolve duplicates before seeding.");
            var country = matches.SingleOrDefault();
            if (country == null) { country = new CountryMaster { Code = seed.Code, Name = seed.Name }; repository.AddCountry(country); existing.Add(country); added++; }
            countries.Add(seed.Code, country);
        }
        return (countries, added);
    }
}
