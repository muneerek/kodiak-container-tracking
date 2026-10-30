using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Globalization;
namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public sealed class MasterRepository(TrackingDbContext db) : IMasterRepository
{
    private IQueryable<T> Query<T>() where T : MasterRecord => db.Set<T>();
    private Task<MasterRecord?> Find<T>(int? id, string? code, CancellationToken ct) where T : MasterRecord => FindCore(Query<T>(), id, code, ct);
    private static async Task<MasterRecord?> FindCore<T>(IQueryable<T> query, int? id, string? code, CancellationToken ct) where T : MasterRecord =>
        await query.SingleOrDefaultAsync(x => id == null ? x.Code == code : x.Id == id, ct);
    public Task<MasterRecord?> GetAsync(MasterKind kind, int id, CancellationToken ct) => FindByKind(kind, id, null, ct);
    public Task<MasterRecord?> GetByCodeAsync(MasterKind kind, string code, CancellationToken ct) => FindByKind(kind, null, code, ct);
    private Task<MasterRecord?> FindByKind(MasterKind kind, int? id, string? code, CancellationToken ct) => kind switch
    {
        MasterKind.ShippingPorts => Find<ShippingPort>(id, code, ct),
        MasterKind.Companies => Find<Company>(id, code, ct), MasterKind.Designations => Find<Designation>(id, code, ct),
        MasterKind.Organizations => Find<OrganizationHierarchy>(id, code, ct), MasterKind.Countries => Find<CountryMaster>(id, code, ct),
        MasterKind.States => Find<StateMaster>(id, code, ct), MasterKind.BusinessCategories => Find<BusinessCategory>(id, code, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public Task<(IReadOnlyList<MasterRecord> Items, int Total)> ListAsync(MasterKind kind, int page, int pageSize, CancellationToken ct) => kind switch
    {
        MasterKind.ShippingPorts => List(Query<ShippingPort>(), page, pageSize, ct),
        MasterKind.Companies => List(Query<Company>(), page, pageSize, ct), MasterKind.Designations => List(Query<Designation>(), page, pageSize, ct),
        MasterKind.Organizations => List(Query<OrganizationHierarchy>(), page, pageSize, ct), MasterKind.Countries => List(Query<CountryMaster>(), page, pageSize, ct),
        MasterKind.States => List(Query<StateMaster>(), page, pageSize, ct), MasterKind.BusinessCategories => List(Query<BusinessCategory>(), page, pageSize, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static async Task<(IReadOnlyList<MasterRecord>, int)> List<T>(IQueryable<T> query, int page, int pageSize, CancellationToken ct) where T : MasterRecord =>
        ((await query.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct)).Cast<MasterRecord>().ToArray(), await query.CountAsync(ct));
    public async Task<IReadOnlyList<TimeZoneMaster>> ListTimeZonesAsync(CancellationToken ct) =>
        await db.TimeZones.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    public Task<bool> TimeZoneExistsAsync(string name, CancellationToken ct) => db.TimeZones.AnyAsync(x => x.Name == name, ct);
    public async Task<string> PreviewShippingPortCodeAsync(CancellationToken ct)
    {
        var number = await db.Set<MasterCodeCounter>().Where(x => x.Kind == "ShippingPorts")
            .Select(x => x.LastNumber).SingleAsync(ct);
        string code;
        do { code = "PORT-" + (++number).ToString("D6", CultureInfo.InvariantCulture); }
        while (await db.ShippingPorts.AnyAsync(x => x.Code == code, ct));
        return code;
    }
    public async Task<string> NextShippingPortCodeAsync(CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("Code allocation requires a transaction.");
        var table = db.Database.IsNpgsql() ? "ctracking.\"masterCodeCounters\"" : "\"masterCodeCounters\"";
        command.CommandText = $"UPDATE {table} SET \"lastNumber\" = \"lastNumber\" + 1 WHERE kind = 'ShippingPorts' RETURNING \"lastNumber\"";
        string code;
        do
        {
            var number = Convert.ToInt64(await command.ExecuteScalarAsync(ct)
                ?? throw new InvalidOperationException("Shipping port counter is missing."));
            code = "PORT-" + number.ToString("D6", CultureInfo.InvariantCulture);
        } while (await db.ShippingPorts.AnyAsync(x => x.Code == code, ct));
        return code;
    }
    public void Add(MasterRecord record) => db.Add(record);
}
