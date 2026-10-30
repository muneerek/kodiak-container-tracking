using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
namespace Kodiak.ContainerTracking.Business.Seeding;

public sealed class StateProvinceSeeder(IGeographySeedRepository repository)
{
    internal async Task<int> SeedAsync(IReadOnlyDictionary<string, CountryMaster> countries, CancellationToken ct)
    {
        var existing = await repository.StatesAsync(ct); var added = 0;
        foreach (var seed in GeographySeedData.States)
        {
            var country = countries[seed.CountryCode];
            var codeMatches = existing.Where(x => string.Equals(x.Code, seed.Code, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (codeMatches.Any(x => x.Country != country)) throw new BusinessException(409, "state_seed_conflict", $"State code {seed.Code} is linked to another country. No seed records were saved.");
            var matches = existing.Where(x => codeMatches.Contains(x) || (x.Country == country &&
                (GeographySeedData.Normalize(x.Name) == GeographySeedData.Normalize(seed.Name)
                 || string.Equals(x.Code, seed.Code[(seed.Code.IndexOf('-') + 1)..], StringComparison.OrdinalIgnoreCase)))).ToArray();
            if (matches.Length > 1) throw new BusinessException(409, "state_seed_conflict", $"Multiple state records match {seed.Code}. Resolve duplicates before seeding.");
            if (matches.Length == 1) continue;
            var state = new StateMaster { Code = seed.Code, Name = seed.Name, Country = country };
            repository.AddState(state); existing.Add(state); added++;
        }
        return added;
    }
}
