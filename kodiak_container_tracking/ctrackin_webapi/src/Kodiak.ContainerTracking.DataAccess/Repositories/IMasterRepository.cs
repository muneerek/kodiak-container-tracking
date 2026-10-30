using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public enum MasterKind { Companies, Designations, Organizations, Countries, States, BusinessCategories, ShippingPorts }
public interface IMasterRepository
{
    Task<MasterRecord?> GetAsync(MasterKind kind, int id, CancellationToken ct);
    Task<MasterRecord?> GetByCodeAsync(MasterKind kind, string code, CancellationToken ct);
    Task<(IReadOnlyList<MasterRecord> Items, int Total)> ListAsync(MasterKind kind, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<TimeZoneMaster>> ListTimeZonesAsync(CancellationToken ct);
    Task<bool> TimeZoneExistsAsync(string name, CancellationToken ct);
    Task<string> PreviewShippingPortCodeAsync(CancellationToken ct);
    Task<string> NextShippingPortCodeAsync(CancellationToken ct);
    void Add(MasterRecord record);
}
