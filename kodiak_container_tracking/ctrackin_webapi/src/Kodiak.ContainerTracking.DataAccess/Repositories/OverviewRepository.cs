using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Kodiak.ContainerTracking.DataAccess.Repositories;
public sealed record OverviewCounts(int Total, int Active, int Cancelled, int InTransit, int Delivered, int OnHold, int Overdue, int Attention, int Shippers, int Consignees, int Routes, int Countries);
public sealed record OverviewStage(string Key, int Count);
public sealed record OverviewShipment(int Id, string Code, string? ContainerNumber, string ShipperName, string Origin, string Destination, DateOnly ExpectedArrival, RegistrationStatus Status, ContainerTrackingStatus TrackingStatus, bool IsOnHold);
public sealed record OverviewRoute(string Origin, string Destination, int Containers, int OriginPortId, int DestinationPortId);
public sealed record OverviewActivity(int ContainerId, string Code, string Kind, ContainerTrackingStatus Status, string Location, DateTimeOffset OccurredAt, DateTimeOffset RecordedAt);
public sealed record OverviewPartner(int Id, string Code, string Name, BusinessStatus Status, int Containers);
public sealed record OverviewSnapshot(OverviewCounts Counts, IReadOnlyList<OverviewStage> Stages, IReadOnlyList<OverviewShipment> Recent, IReadOnlyList<OverviewShipment> Attention, IReadOnlyList<OverviewRoute> Routes, IReadOnlyList<OverviewActivity> Activity, IReadOnlyList<OverviewPartner> Partners, int PartnerCount);
public interface IOverviewRepository { Task<OverviewSnapshot> ReadAsync(DateOnly today, CancellationToken ct, ContainerScope? scope = null); }
public sealed class OverviewRepository(TrackingDbContext db) : IOverviewRepository
{
    public async Task<OverviewSnapshot> ReadAsync(DateOnly today, CancellationToken ct, ContainerScope? scope = null)
    {
        scope ??= new ContainerScope(true);
        var all = db.ContainerRegistrations.AsNoTracking().Where(x => scope.IsAdmin || (scope.ShipperId != null && x.ShipperId == scope.ShipperId) || (scope.ConsigneeId != null && x.ConsigneeId == scope.ConsigneeId));
        var visibleShippers = db.Shippers.AsNoTracking().Where(x => scope.IsAdmin || (scope.ShipperId != null && x.UniqueId == scope.ShipperId) || (scope.ConsigneeId != null && x.ConsigneeId == scope.ConsigneeId));
        var visibleConsignees = db.Consignees.AsNoTracking().Where(x => scope.IsAdmin || (scope.ConsigneeId != null && x.UniqueId == scope.ConsigneeId)
            || (scope.ShipperId != null && db.Shippers.Any(shipper => shipper.UniqueId == scope.ShipperId && shipper.ConsigneeId == x.UniqueId)));
        var live = all.Where(x => x.Status == RegistrationStatus.Booked);
        var open = live.Where(x => x.TrackingStatus != ContainerTrackingStatus.Delivered);
        var attention = open.Where(x => x.IsOnHold || x.ExpectedArrival < today || x.ContainerNumber == null);
        var summary = await all.GroupBy(x => 1).Select(g => new {
            Total = g.Count(), Cancelled = g.Count(x => x.Status == RegistrationStatus.Cancelled),
            InTransit = g.Count(x => x.Status == RegistrationStatus.Booked && x.TrackingStatus == ContainerTrackingStatus.InTransit),
            Delivered = g.Count(x => x.Status == RegistrationStatus.Booked && x.TrackingStatus == ContainerTrackingStatus.Delivered),
            OnHold = g.Count(x => x.Status == RegistrationStatus.Booked && x.IsOnHold)
        }).SingleOrDefaultAsync(ct);
        var stageRows = await all.GroupBy(x => x.Status == RegistrationStatus.Cancelled ? "cancelled" : x.IsOnHold ? "hold" : ((int)x.TrackingStatus).ToString())
            .Select(g => new OverviewStage(g.Key, g.Count())).ToListAsync(ct);
        var routes = live.GroupBy(x => new { x.OriginPortId, x.DestinationPortId, Origin = x.OriginPort.Name, Destination = x.DestinationPort.Name });
        var routeCount = await live.Select(x => new {x.OriginPortId,x.DestinationPortId}).Distinct().CountAsync(ct);
        var topRoutes = await routes.OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Origin).ThenBy(g => g.Key.Destination).Take(4)
            .Select(g => new OverviewRoute(g.Key.Origin,g.Key.Destination,g.Count(),g.Key.OriginPortId,g.Key.DestinationPortId)).ToListAsync(ct);
        var countries = await live.Select(x => x.OriginPort.CountryId).Union(live.Select(x => x.DestinationPort.CountryId)).CountAsync(ct);
        var counts = new OverviewCounts(summary?.Total ?? 0,(summary?.Total ?? 0)-(summary?.Cancelled ?? 0),summary?.Cancelled ?? 0,
            summary?.InTransit ?? 0,summary?.Delivered ?? 0,summary?.OnHold ?? 0,await open.CountAsync(x => x.ExpectedArrival < today,ct),await attention.CountAsync(ct),
            await visibleShippers.CountAsync(x => x.Status == BusinessStatus.Active,ct),await visibleConsignees.CountAsync(x => x.Status == BusinessStatus.Active,ct),routeCount,countries);
        var newestRegistrations = db.Database.IsNpgsql() ? all.OrderByDescending(x => x.CreateOn).ThenByDescending(x => x.Id) : all.OrderByDescending(x => x.Id);
        var recent = await Shipments(newestRegistrations.Take(5)).ToListAsync(ct);
        var needsAttention = await Shipments(attention.OrderByDescending(x => x.IsOnHold).ThenBy(x => x.ExpectedArrival).ThenBy(x => x.Id).Take(5)).ToListAsync(ct);
        var events = db.Set<ContainerTrackingEvent>().AsNoTracking().Where(x => all.Any(container => container.Id == x.ContainerRegistrationId));
        var newestEvents = db.Database.IsNpgsql() ? events.OrderByDescending(x => x.CreateOn).ThenByDescending(x => x.Id) : events.OrderByDescending(x => x.Id);
        var updates = await newestEvents.Take(6)
            .Select(x => new OverviewActivity(x.ContainerRegistrationId,x.Registration.Code,"tracking",x.Status,x.Location,x.EventAt,x.CreateOn)).ToListAsync(ct);
        var registrations = await newestRegistrations.Take(6)
            .Select(x => new OverviewActivity(x.Id,x.Code,"registration",ContainerTrackingStatus.Booked,x.OriginPort.Name,x.CreateOn,x.CreateOn)).ToListAsync(ct);
        var partnerCount = scope.ConsigneeId != null ? await visibleShippers.CountAsync(ct) : 0;
        var partners = scope.ConsigneeId != null
            ? await visibleShippers.OrderBy(x => x.Name).ThenBy(x => x.UniqueId).Take(6)
                .Select(x => new OverviewPartner(x.UniqueId,x.UniqueCode,x.Name,x.Status,all.Count(container => container.ShipperId == x.UniqueId))).ToListAsync(ct)
            : new List<OverviewPartner>();
        return new(counts,stageRows,recent,needsAttention,topRoutes,updates.Concat(registrations).OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.ContainerId).Take(6).ToArray(),partners,partnerCount);
    }
    private static IQueryable<OverviewShipment> Shipments(IQueryable<ContainerRegistration> query) => query.Select(x => new OverviewShipment(x.Id,x.Code,x.ContainerNumber,
        x.Shipper.Name,x.OriginPort.Name,x.DestinationPort.Name,x.ExpectedArrival,x.Status,x.TrackingStatus,x.IsOnHold));
}
