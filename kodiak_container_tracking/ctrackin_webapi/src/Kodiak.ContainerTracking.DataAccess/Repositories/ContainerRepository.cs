using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Kodiak.ContainerTracking.DataAccess.Repositories;
public sealed record ContainerScope(bool IsAdmin, int? ShipperId = null, int? ConsigneeId = null);
public sealed class ContainerOption
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ConsigneeName { get; init; }
}
public interface IContainerRepository
{
    Task<(IReadOnlyList<ContainerRegistration> Items, int Total)> ListAsync(ContainerScope scope, int page, int size, string? search, RegistrationStatus? status, CancellationToken ct, ContainerTrackingStatus? trackingStatus = null, bool? onHold = null, int? shipperId = null);
    Task<ContainerRegistration?> GetAsync(int id, ContainerScope scope, CancellationToken ct);
    Task<bool> NumberExistsAsync(string number, int exceptId, CancellationToken ct);
    Task<long> NextNumberAsync(CancellationToken ct);
    Task<(IReadOnlyList<ContainerOption> Items, int Total)> OptionsAsync(string kind, ContainerScope scope, int page, int size, CancellationToken ct);
    void Add(ContainerRegistration registration);
}
public sealed class ContainerRepository(TrackingDbContext db) : IContainerRepository
{
    private IQueryable<ContainerRegistration> Query(ContainerScope scope) => db.ContainerRegistrations
        .Where(x => scope.IsAdmin || (scope.ShipperId != null && x.ShipperId == scope.ShipperId) || (scope.ConsigneeId != null && x.ConsigneeId == scope.ConsigneeId))
        .Include(x => x.Shipper).Include(x => x.Consignee).Include(x => x.OriginPort).Include(x => x.DestinationPort).Include(x => x.Attachments).Include(x => x.TrackingEvents).AsSplitQuery();
    public async Task<(IReadOnlyList<ContainerRegistration> Items, int Total)> ListAsync(ContainerScope scope, int page, int size, string? search, RegistrationStatus? status, CancellationToken ct, ContainerTrackingStatus? trackingStatus = null, bool? onHold = null, int? shipperId = null)
    {
        var query = Query(scope).AsNoTracking();
        if (trackingStatus.HasValue) query = query.Where(x => x.TrackingStatus == trackingStatus.Value);
        if (onHold.HasValue) query = query.Where(x => x.IsOnHold == onHold.Value);
        if (shipperId.HasValue) query = query.Where(x => x.ShipperId == shipperId.Value);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToUpperInvariant(); query = query.Where(x => x.Code.Contains(term) || (x.ContainerNumber != null && x.ContainerNumber.Contains(term)) || x.Shipper.Name.ToUpper().Contains(term) || x.OriginPort.Name.ToUpper().Contains(term) || x.OriginPort.Code.ToUpper().Contains(term) || x.DestinationPort.Name.ToUpper().Contains(term) || x.DestinationPort.Code.ToUpper().Contains(term) || (x.Reference != null && x.Reference.ToUpper().Contains(term))); }
        var total = await query.CountAsync(ct); return (await query.OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct), total);
    }
    public Task<ContainerRegistration?> GetAsync(int id, ContainerScope scope, CancellationToken ct) => Query(scope).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> NumberExistsAsync(string number, int exceptId, CancellationToken ct) => db.ContainerRegistrations.AnyAsync(x => x.Id != exceptId && x.ContainerNumber == number && x.Status == RegistrationStatus.Booked, ct);
    public async Task<long> NextNumberAsync(CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return (await db.ContainerRegistrations.MaxAsync(x => (int?)x.Id, ct) ?? 0) + 1;
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT nextval('ctracking.\"containerRegistrationNumbers\"')";
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }
    public async Task<(IReadOnlyList<ContainerOption> Items, int Total)> OptionsAsync(string kind, ContainerScope scope, int page, int size, CancellationToken ct)
    {
        var query = kind == "tracking-shippers"
            ? db.ContainerRegistrations.Where(x => scope.IsAdmin || (scope.ShipperId != null && x.ShipperId == scope.ShipperId) || (scope.ConsigneeId != null && x.ConsigneeId == scope.ConsigneeId))
                .Select(x => new { x.ShipperId, x.Shipper.Name, x.Shipper.UniqueCode })
                .Union(db.Shippers.Where(x => scope.ConsigneeId != null && x.ConsigneeId == scope.ConsigneeId).Select(x => new { ShipperId=x.UniqueId, x.Name, x.UniqueCode }))
                .Distinct()
                .Select(x => new ContainerOption { Id = x.ShipperId, Name = x.Name, Code = x.UniqueCode })
            : kind == "ports"
            ? db.ShippingPorts.Where(x => x.IsActive && x.Country.IsActive && (x.State == null || x.State.IsActive)).Select(x => new ContainerOption { Id = x.Id, Code = x.Code, Name = x.Name })
            : db.Shippers.Where(x => x.Status == BusinessStatus.Active && (scope.IsAdmin || x.UniqueId == scope.ShipperId)).Select(x => new ContainerOption { Id = x.UniqueId, Code = x.UniqueCode, Name = x.Name, ConsigneeName = x.Consignee == null ? null : x.Consignee.Name });
        var total = await query.CountAsync(ct); return (await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct), total);
    }
    public void Add(ContainerRegistration registration) => db.Add(registration);
}
