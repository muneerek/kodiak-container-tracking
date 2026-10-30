using Kodiak.ContainerTracking.DataAccess.Entities;

namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public interface IPartyRepository
{
    Task<PartyReference?> GetReferenceAsync(long uniqueId, CancellationToken ct);
    Task<PartyReference?> GetByCodeAsync(string code, CancellationToken ct);
    Task<(IReadOnlyList<PartyRecord> Items, int Total)> ListAsync(PartyType type, int page, int pageSize, CancellationToken ct, bool includeSuperAdmin = true, string? search = null);
    Task<long> NextCodeNumberAsync(PartyType type, CancellationToken ct);
    void Add(PartyRecord party);
}
