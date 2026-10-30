using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.DataAccess.Entities;

namespace Kodiak.ContainerTracking.Business.Services;

public interface IPartyService
{
    Task<PagedResponse<PartyResponse>> ListAsync(PartyType type, int page, int pageSize, CancellationToken ct, string? search = null);
    Task<PartyResponse> GetAsync(PartyType type, long id, CancellationToken ct);
    Task<PartyResponse> GetByCodeAsync(string code, CancellationToken ct);
    Task<PartyResponse> CreateEmployeeAsync(EmployeeRequest request, CancellationToken ct);
    Task<PartyResponse> UpdateEmployeeAsync(long id, long version, EmployeeRequest request, CancellationToken ct);
    Task<PartyResponse> SetEmployeePhotoAsync(long id, long version, string? path, CancellationToken ct);
    Task<PartyResponse> TerminateEmployeeAsync(long id, long version, CancellationToken ct);
    Task<PartyResponse> DeactivateConsigneeAsync(long id, long version, CancellationToken ct);
    Task<PartyResponse> CreateCompanyAsync(PartyType type, CompanyRequest request, CancellationToken ct);
    Task<PartyResponse> UpdateCompanyAsync(PartyType type, long id, long version, CompanyRequest request, CancellationToken ct);
    Task<PartyResponse> SetShipperLogoAsync(long id, long version, string? path, CancellationToken ct);
    Task<PartyResponse> DeactivateShipperAsync(long id, long version, CancellationToken ct);
    Task<PartyResponse> CreateShipperAsync(ShipperRequest request, CancellationToken ct);
    Task<PartyResponse> UpdateShipperAsync(long id, long version, ShipperRequest request, CancellationToken ct);
}
