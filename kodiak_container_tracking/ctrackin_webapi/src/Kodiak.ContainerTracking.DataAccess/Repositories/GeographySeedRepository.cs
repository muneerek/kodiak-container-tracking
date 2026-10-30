using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public interface IGeographySeedRepository
{
    Task<List<CountryMaster>> CountriesAsync(CancellationToken ct);
    Task<List<StateMaster>> StatesAsync(CancellationToken ct);
    void AddCountry(CountryMaster country);
    void AddState(StateMaster state);
}
public sealed class GeographySeedRepository(TrackingDbContext db) : IGeographySeedRepository
{
    public Task<List<CountryMaster>> CountriesAsync(CancellationToken ct) => db.Set<CountryMaster>().ToListAsync(ct);
    public Task<List<StateMaster>> StatesAsync(CancellationToken ct) => db.Set<StateMaster>().Include(x => x.Country).ToListAsync(ct);
    public void AddCountry(CountryMaster country) => db.Add(country);
    public void AddState(StateMaster state) => db.Add(state);
}
