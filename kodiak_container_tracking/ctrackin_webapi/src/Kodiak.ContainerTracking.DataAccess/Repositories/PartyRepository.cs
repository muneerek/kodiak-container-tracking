using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public sealed class PartyRepository(TrackingDbContext db) : IPartyRepository
{
    private IQueryable<PartyReference> References => db.PartyReferences
        .Include(x => x.Employee).Include(x => x.Consignee).Include(x => x.Shipper).ThenInclude(x => x!.Consignee);

    public Task<PartyReference?> GetReferenceAsync(long uniqueId, CancellationToken ct) =>
        References.SingleOrDefaultAsync(x => x.UniqueId == uniqueId, ct);

    public Task<PartyReference?> GetByCodeAsync(string code, CancellationToken ct) =>
        References.SingleOrDefaultAsync(x => x.UniqueCode == code, ct);

    public Task<(IReadOnlyList<PartyRecord> Items, int Total)> ListAsync(PartyType type, int page, int pageSize, CancellationToken ct, bool includeSuperAdmin = true, string? search = null) => type switch
    {
        PartyType.Employee => ListCoreAsync(db.Employees.AsNoTracking().Where(e => includeSuperAdmin ||
            ((e.Designation == null || e.Designation.Code != "SUPERADMIN") && !db.Users.Any(u => u.LinkedUniqueId == e.UniqueId && u.RoleMappings.Any(m => m.Role.NormalizedCode == "SUPERADMIN")))), page, pageSize, ct),
        PartyType.Consignee => ListCoreAsync(db.Consignees.AsNoTracking(), page, pageSize, ct, search),
        PartyType.Shipper => ListCoreAsync(db.Shippers.AsNoTracking().Include(x => x.Consignee), page, pageSize, ct, search),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static async Task<(IReadOnlyList<PartyRecord>, int)> ListCoreAsync<T>(IQueryable<T> query, int page, int pageSize, CancellationToken ct, string? search = null) where T : PartyRecord
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.UniqueCode.ToLower().Contains(term)
                || (x.EmailId != null && x.EmailId.ToLower().Contains(term)) || (x.Address != null && x.Address.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.UniqueId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items.Cast<PartyRecord>().ToArray(), total);
    }

    public async Task<long> NextCodeNumberAsync(PartyType type, CancellationToken ct)
    {
        // Reserve the number before inserting the code, inside the owning transaction.
        // This avoids a duplicate insert reaching the unique index before an EF counter update.
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("Code allocation requires a transaction.");
        var table = db.Database.IsNpgsql() ? "ctracking.\"uniqueCodeCounters\"" : "\"uniqueCodeCounters\"";
        command.CommandText = $"UPDATE {table} SET \"lastNumber\" = \"lastNumber\" + 1 WHERE \"partyType\" = @type RETURNING \"lastNumber\"";
        var parameter = command.CreateParameter(); parameter.ParameterName = "type"; parameter.Value = (int)type; command.Parameters.Add(parameter);
        var value = await command.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Code counter is missing.");
        return Convert.ToInt64(value);
    }

    public void Add(PartyRecord party)
    {
        switch (party)
        {
            case Employee employee: db.Employees.Add(employee); break;
            case Consignee consignee: db.Consignees.Add(consignee); break;
            case Shipper shipper: db.Shippers.Add(shipper); break;
            default: throw new ArgumentOutOfRangeException(nameof(party));
        }
    }

}
