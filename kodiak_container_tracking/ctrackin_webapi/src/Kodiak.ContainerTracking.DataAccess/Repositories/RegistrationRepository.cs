using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public sealed record RegistrationSelection
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}
public interface IRegistrationRepository
{
    Task<(IReadOnlyList<RegistrationSelection> Items, int Total)> OptionsAsync(string kind, string? search, int page, int pageSize, CancellationToken ct);
}
public sealed class RegistrationRepository(TrackingDbContext db) : IRegistrationRepository
{
    public async Task<(IReadOnlyList<RegistrationSelection> Items, int Total)> OptionsAsync(string kind, string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = kind switch
        {
            "consignees" => db.Consignees.AsNoTracking().Where(x => x.Status == BusinessStatus.Active).Select(x => new RegistrationSelection { Id = x.UniqueId, Code = x.UniqueCode, Name = x.Name }),
            "countries" => db.Set<CountryMaster>().AsNoTracking().Where(x => x.IsActive).Select(x => new RegistrationSelection { Id = x.Id, Code = x.Code, Name = x.Name }),
            "businesscategories" => db.Set<BusinessCategory>().AsNoTracking().Where(x => x.IsActive).Select(x => new RegistrationSelection { Id = x.Id, Code = x.Code, Name = x.Name }),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToUpperInvariant(); query = query.Where(x => x.Name.ToUpper().Contains(term) || x.Code.ToUpper().Contains(term)); }
        var total = await query.CountAsync(ct);
        return (await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), total);
    }
}
