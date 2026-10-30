using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
namespace Kodiak.ContainerTracking.Business.Services;

public interface IMasterService
{
    Task<PagedResponse<MasterResponse>> ListAsync(MasterKind kind, int page, int pageSize, CancellationToken ct);
    Task<string> PreviewShippingPortCodeAsync(CancellationToken ct);
    Task<IReadOnlyList<TimeZoneResponse>> ListTimeZonesAsync(CancellationToken ct);
    Task<MasterResponse> GetAsync(MasterKind kind, int id, CancellationToken ct);
    Task<MasterResponse> CreateAsync(MasterKind kind, MasterRequest request, CancellationToken ct);
    Task<MasterResponse> UpdateAsync(MasterKind kind, int id, long version, MasterRequest request, CancellationToken ct);
    Task<MasterResponse> DeactivateAsync(MasterKind kind, int id, long version, CancellationToken ct);
    Task<MasterResponse> DeactivateBusinessCategoryAsync(int id, long version, CancellationToken ct);
}

public sealed class MasterService(IMasterRepository repository, IUnitOfWork unitOfWork, ICurrentActor actor) : IMasterService
{
    public Task<string> PreviewShippingPortCodeAsync(CancellationToken ct) => repository.PreviewShippingPortCodeAsync(ct);
    public async Task<IReadOnlyList<TimeZoneResponse>> ListTimeZonesAsync(CancellationToken ct) =>
        (await repository.ListTimeZonesAsync(ct)).Select(x => new TimeZoneResponse(x.Name, x.Abbreviation, x.UtcOffsetSeconds, x.IsDaylightSaving, x.ObservedAt)).ToArray();
    public async Task<PagedResponse<MasterResponse>> ListAsync(MasterKind kind, int page, int pageSize, CancellationToken ct)
    {
        ValidateKind(kind); ServiceValidation.Page(page, pageSize);
        var (items, total) = await repository.ListAsync(kind, page, pageSize, ct);
        return new(items.Select(x => x.ToResponse()).ToArray(), total, page, pageSize);
    }
    public async Task<MasterResponse> GetAsync(MasterKind kind, int id, CancellationToken ct) { ValidateKind(kind); return (await Find(kind, id, ct)).ToResponse(); }
    public Task<MasterResponse> CreateAsync(MasterKind kind, MasterRequest request, CancellationToken ct)
    {
        ValidateKind(kind); ServiceValidation.Validate(request);
        return unitOfWork.InTransactionAsync(async () =>
        {
            MasterRecord record = kind switch
            {
                MasterKind.Companies => new Company(), MasterKind.Designations => new Designation(), MasterKind.Organizations => new OrganizationHierarchy(),
                MasterKind.ShippingPorts => new ShippingPort(), MasterKind.Countries => new CountryMaster(), MasterKind.States => new StateMaster(), MasterKind.BusinessCategories => new BusinessCategory(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            await Apply(kind, record, request, ct);
            repository.Add(record);
            await unitOfWork.SaveAsync(actor.UserId, actor.Subject, $"Create {kind}", ct);
            return record.ToResponse();
        }, ct);
    }
    public Task<MasterResponse> UpdateAsync(MasterKind kind, int id, long version, MasterRequest request, CancellationToken ct)
    {
        ValidateKind(kind); ServiceValidation.Validate(request);
        return unitOfWork.InTransactionAsync(async () =>
        {
            var record = await Find(kind, id, ct); ServiceValidation.Version(version, record.Version);
            await Apply(kind, record, request, ct); record.Version++;
            await unitOfWork.SaveAsync(actor.UserId, actor.Subject, $"Update {kind}", ct);
            return record.ToResponse();
        }, ct);
    }
    public Task<MasterResponse> DeactivateBusinessCategoryAsync(int id, long version, CancellationToken ct) => unitOfWork.InTransactionAsync(async () =>
    {
        var record = await Find(MasterKind.BusinessCategories, id, ct); ServiceValidation.Version(version, record.Version);
        record.IsActive = false; record.Version++;
        await unitOfWork.SaveAsync(actor.UserId, actor.Subject, "Deactivate business category", ct);
        return record.ToResponse();
    }, ct);
    public Task<MasterResponse> DeactivateAsync(MasterKind kind, int id, long version, CancellationToken ct)
    {
        if (kind is not (MasterKind.Countries or MasterKind.States or MasterKind.ShippingPorts))
            throw new BusinessException(400, "unsupported_action", "Deactivation is not supported through this endpoint for this master.");
        return unitOfWork.InTransactionAsync(async () =>
        {
            var record = await Find(kind, id, ct); ServiceValidation.Version(version, record.Version);
            record.IsActive = false; record.Version++;
            await unitOfWork.SaveAsync(actor.UserId, actor.Subject, $"Deactivate {kind}", ct); return record.ToResponse();
        }, ct);
    }
    private static void ValidateKind(MasterKind kind) { if (!Enum.IsDefined(kind)) throw new BusinessException(400, "invalid_master_kind", "Unknown master kind."); }
    private async Task Apply(MasterKind kind, MasterRecord record, MasterRequest request, CancellationToken ct)
    {
        var code = kind == MasterKind.ShippingPorts
            ? record.Id == 0 ? await repository.NextShippingPortCodeAsync(ct) : record.Code
            : ServiceValidation.Required(request.Code ?? "", "Code").ToUpperInvariant();
        if (kind == MasterKind.Designations && (code == "SUPERADMIN" || record.Code == "SUPERADMIN"))
            throw new BusinessException(409, "protected_designation", "The SUPERADMIN designation is managed by technical bootstrap.");
        var existing = await repository.GetByCodeAsync(kind, code, ct);
        if (existing != null && existing.Id != record.Id) throw new BusinessException(409, "duplicate_code", "Code is already in use.");
        record.Code = code; record.Name = ServiceValidation.Required(request.Name, "Name"); record.IsActive = request.IsActive;
        if (record is StateMaster state)
        {
            if (state.Id != 0 && state.CountryId != request.CountryId) throw new BusinessException(409, "immutable_country", "An existing state cannot be moved to another country.");
            await Require(MasterKind.Countries, request.CountryId, ct); state.CountryId = request.CountryId!.Value;
        }
        if (record is ShippingPort port)
        {
            await Require(MasterKind.Countries, request.CountryId, ct);
            if (request.StateId != null)
            {
                var selectedState = (StateMaster)await Require(MasterKind.States, request.StateId, ct);
                if (selectedState.CountryId != request.CountryId) throw new BusinessException(400, "invalid_state", "State must belong to the selected country.");
            }
            var zone = ServiceValidation.Optional(request.TimeZone) ?? port.TimeZone;
            if (!await repository.TimeZoneExistsAsync(zone, ct)) throw new BusinessException(400, "invalid_time_zone", "Enter a valid time zone, for example UTC, Asia/Kolkata or Asia/Dubai.");
            port.CountryId = request.CountryId!.Value; port.StateId = request.StateId;
            port.City = ServiceValidation.Optional(request.City); port.TimeZone = zone;
        }
        if (record is Company company)
        {
            if (request.CountryId != null) await Require(MasterKind.Countries, request.CountryId, ct);
            if (request.StateId != null)
            {
                var selected = (StateMaster)await Require(MasterKind.States, request.StateId, ct);
                if (selected.CountryId != request.CountryId) throw new BusinessException(400, "invalid_state", "State must belong to the selected country.");
            }
            company.CountryId = request.CountryId; company.StateId = request.StateId; company.EmailId = request.EmailId;
            company.PhoneNumber = request.PhoneNumber; company.Address = request.Address; company.Logo = request.Logo;
        }
        if (record is OrganizationHierarchy org)
        {
            await Require(MasterKind.Companies, request.CompanyId, ct);
            var visited = new HashSet<int>(); var parent = request.ParentOrgId;
            while (parent != null)
            {
                if (parent == org.Id || !visited.Add(parent.Value)) throw new BusinessException(400, "organization_cycle", "Organization hierarchy cannot contain a cycle.");
                var node = (OrganizationHierarchy)await Require(MasterKind.Organizations, parent, ct);
                if (node.CompanyId != request.CompanyId) throw new BusinessException(400, "organization_company", "Parent organization must belong to the same company.");
                parent = node.ParentOrgId;
            }
            if (org.Id != 0 && org.CompanyId != request.CompanyId) throw new BusinessException(409, "immutable_company", "An existing organization cannot be moved to another company.");
            org.CompanyId = request.CompanyId!.Value; org.ParentOrgId = request.ParentOrgId;
        }
    }
    private async Task<MasterRecord> Require(MasterKind kind, int? id, CancellationToken ct) => id is > 0 && await repository.GetAsync(kind, id.Value, ct) is { IsActive: true } record
        ? record : throw new BusinessException(400, "invalid_master_reference", $"Select an active {kind} record.");
    private async Task<MasterRecord> Find(MasterKind kind, int id, CancellationToken ct) => await repository.GetAsync(kind, id, ct)
        ?? throw new BusinessException(404, "master_not_found", "Master record was not found.");
}
