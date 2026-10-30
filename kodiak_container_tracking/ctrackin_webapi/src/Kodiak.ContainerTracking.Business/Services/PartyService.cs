using System.Globalization;
using Kodiak.ContainerTracking.Business.Configuration;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
using Microsoft.Extensions.Options;
namespace Kodiak.ContainerTracking.Business.Services;

public sealed class PartyService(IPartyRepository repository, IMasterRepository masters, IUserManagementRepository users,
    IUnitOfWork unitOfWork, ICurrentActor actor, IOptions<UniqueCodeOptions> options, TechnicalAccountAccess access, ShipperAccess shipperAccess) : IPartyService
{
    public async Task<PagedResponse<PartyResponse>> ListAsync(PartyType type, int page, int pageSize, CancellationToken ct, string? search = null)
    {
        ServiceValidation.Page(page, pageSize);
        if (type == PartyType.Shipper && !actor.IsAdmin)
        {
            var own = await GetAsync(type, await shipperAccess.OwnIdAsync(ct), ct);
            var term = search?.Trim();
            var matches = string.IsNullOrEmpty(term) || new[] { own.CompanyName, own.UniqueCode, own.EmailId, own.Address }
                .Any(value => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
            return new(page == 1 && matches ? new[] { own } : Array.Empty<PartyResponse>(), matches ? 1 : 0, page, pageSize);
        }
        var (items, total) = await repository.ListAsync(type, page, pageSize, ct, actor.IsSuperAdmin, search);
        return new(items.Select(x => x.ToResponse()).ToArray(), total, page, pageSize);
    }
    public async Task<PartyResponse> GetAsync(PartyType type, long id, CancellationToken ct) => (await Find(type, id, ct)).ToResponse();
    public async Task<PartyResponse> GetByCodeAsync(string code, CancellationToken ct)
    {
        var reference = await repository.GetByCodeAsync(ServiceValidation.Required(code, "Code").ToUpperInvariant(), ct);
        if (reference == null) throw Missing();
        await access.RequireEmployeeAsync(reference.UniqueId, ct);
        if (reference.PartyType == PartyType.Shipper) await shipperAccess.RequireAsync(reference.UniqueId, ct);
        return reference.GetParty()?.ToResponse() ?? throw Missing();
    }
    public Task<PartyResponse> CreateEmployeeAsync(EmployeeRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request); ServiceValidation.Dob(request.Dob);
        return Create(PartyType.Employee, async () => { var employee = new Employee(); await ApplyEmployee(employee, request, ct); return employee; }, ct);
    }
    public Task<PartyResponse> UpdateEmployeeAsync(long id, long version, EmployeeRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request); ServiceValidation.Dob(request.Dob);
        return Update(PartyType.Employee, id, version, p => ApplyEmployee((Employee)p, request, ct), ct);
    }
    public Task<PartyResponse> SetEmployeePhotoAsync(long id, long version, string? path, CancellationToken ct) =>
        Update(PartyType.Employee, id, version, record => { record.Logo = path; return Task.CompletedTask; }, ct);

    public Task<PartyResponse> TerminateEmployeeAsync(long id, long version, CancellationToken ct) =>
        Update(PartyType.Employee, id, version, async record =>
        {
            if (await users.WouldRemoveLastAdminForEmployeeAsync(record.UniqueId, ct))
                throw new BusinessException(409, "last_admin", "The final active administrator employee cannot be terminated.");
            ((Employee)record).UserStatus = EmployeeStatus.Terminated;
        }, ct);

    public Task<PartyResponse> DeactivateConsigneeAsync(long id, long version, CancellationToken ct) =>
        Update(PartyType.Consignee, id, version, record => { ((Consignee)record).Status = BusinessStatus.Blacklisted; return Task.CompletedTask; }, ct);

    public Task<PartyResponse> CreateCompanyAsync(PartyType type, CompanyRequest request, CancellationToken ct)
    {
        if (type != PartyType.Consignee) throw new ArgumentOutOfRangeException(nameof(type)); ServiceValidation.Validate(request);
        return Create(type, async () => { var consignee = new Consignee(); await ApplyConsignee(consignee, request, ct); return consignee; }, ct);
    }
    public Task<PartyResponse> UpdateCompanyAsync(PartyType type, long id, long version, CompanyRequest request, CancellationToken ct)
    {
        if (type != PartyType.Consignee) throw new ArgumentOutOfRangeException(nameof(type)); ServiceValidation.Validate(request);
        return Update(type, id, version, p => ApplyConsignee((Consignee)p, request, ct), ct);
    }
    public Task<PartyResponse> CreateShipperAsync(ShipperRequest request, CancellationToken ct)
    {
        shipperAccess.RequireAdmin(); ServiceValidation.Validate(request);
        return Create(PartyType.Shipper, async () => { var shipper = new Shipper(); await ApplyShipper(shipper, request, ct); return shipper; }, ct);
    }
    public Task<PartyResponse> UpdateShipperAsync(long id, long version, ShipperRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request); return Update(PartyType.Shipper, id, version, p => ApplyShipper((Shipper)p, request, ct), ct);
    }
    public Task<PartyResponse> SetShipperLogoAsync(long id, long version, string? path, CancellationToken ct) =>
        Update(PartyType.Shipper, id, version, record => { record.Logo = path; return Task.CompletedTask; }, ct);
    public Task<PartyResponse> DeactivateShipperAsync(long id, long version, CancellationToken ct)
    {
        shipperAccess.RequireAdmin();
        return Update(PartyType.Shipper, id, version, record => { ((Shipper)record).Status = BusinessStatus.Blacklisted; return Task.CompletedTask; }, ct);
    }
    private Task<PartyResponse> Create(PartyType type, Func<Task<PartyRecord>> factory, CancellationToken ct) => unitOfWork.InTransactionAsync(async () =>
    {
        var number = await repository.NextCodeNumberAsync(type, ct);
        var prefix = type switch { PartyType.Employee => options.Value.EmployeePrefix, PartyType.Consignee => options.Value.ConsigneePrefix, _ => options.Value.ShipperPrefix };
        var record = await factory(); record.PartyType = type;
        record.UniqueCode = prefix + number.ToString($"D{options.Value.Padding}", CultureInfo.InvariantCulture);
        record.Reference = new PartyReference { PartyType = type, UniqueCode = record.UniqueCode };
        repository.Add(record); await unitOfWork.SaveAsync(actor.UserId, actor.Subject, $"Create {type}", ct); return record.ToResponse();
    }, ct);
    private Task<PartyResponse> Update(PartyType type, long id, long version, Func<PartyRecord, Task> apply, CancellationToken ct) => unitOfWork.InTransactionAsync(async () =>
    {
        var record = await Find(type, id, ct); ServiceValidation.Version(version, record.Version);
        await apply(record); record.Version++; await unitOfWork.SaveAsync(actor.UserId, actor.Subject, $"Update {type}", ct); return record.ToResponse();
    }, ct);
    private async Task ApplyEmployee(Employee employee, EmployeeRequest request, CancellationToken ct)
    {
        if (request.Logo != employee.Logo && request.Logo?.StartsWith("employees/", StringComparison.OrdinalIgnoreCase) == true)
            throw new BusinessException(400, "photo_endpoint_required", "Use the employee photo upload endpoint to change a stored photograph.");
        if (request.UserStatus == EmployeeStatus.Terminated && employee.UniqueId != 0 && await users.WouldRemoveLastAdminForEmployeeAsync(employee.UniqueId, ct))
            throw new BusinessException(409, "last_admin", "The final active administrator employee cannot be terminated.");
        if (request.DesignationId != null && await masters.GetAsync(MasterKind.Designations, request.DesignationId.Value, ct) is { Code: "SUPERADMIN" } && !actor.IsSuperAdmin)
            throw new BusinessException(403, "protected_designation", "Only a superadmin can assign this designation.");
        await CheckMaster(MasterKind.Designations, request.DesignationId, ct); await CheckMaster(MasterKind.Organizations, request.OrgId, ct);
        employee.FirstName = ServiceValidation.Required(request.FirstName, "First name"); employee.LastName = ServiceValidation.Optional(request.LastName);
        employee.Dob = request.Dob; employee.EmailId = ServiceValidation.Optional(request.EmailId); employee.Logo = ServiceValidation.Optional(request.Logo);
        employee.UserStatus = request.UserStatus; employee.DesignationId = request.DesignationId; employee.OrgId = request.OrgId;
    }
    private async Task ApplyConsignee(Consignee consignee, CompanyRequest request, CancellationToken ct)
    {
        await CheckMaster(MasterKind.Countries, request.CountryId, ct);
        consignee.Name = ServiceValidation.Required(request.CompanyName, "Company name"); consignee.LastName = ServiceValidation.Optional(request.LastName);
        consignee.EmailId = ServiceValidation.Required(request.EmailId, "Email"); consignee.Address = ServiceValidation.Required(request.Address, "Address");
        consignee.PhoneNumber = ServiceValidation.Optional(request.PhoneNumber); consignee.CountryId = request.CountryId; consignee.Logo = ServiceValidation.Optional(request.Logo); consignee.Status = request.Status;
    }
    private async Task ApplyShipper(Shipper shipper, ShipperRequest request, CancellationToken ct)
    {
        if (!actor.IsAdmin && request.Status != shipper.Status) throw new BusinessException(403, "protected_status", "Only administrators can change shipper status.");
        if (request.Logo != shipper.Logo && (request.Logo?.StartsWith("employees/", StringComparison.OrdinalIgnoreCase) == true || request.Logo?.StartsWith("shippers/", StringComparison.OrdinalIgnoreCase) == true))
            throw new BusinessException(400, "logo_endpoint_required", "Use the shipper logo upload endpoint to change the logo.");
        var reference = request.ConsigneeId is int consigneeId ? await repository.GetReferenceAsync(consigneeId, ct) : null;
        if (request.ConsigneeId != null && (reference?.Consignee == null || (reference.Consignee.Status != BusinessStatus.Active && shipper.ConsigneeId != request.ConsigneeId))) throw new BusinessException(400, "invalid_consignee", "Select an active consignee.");
        await CheckMaster(MasterKind.Countries, request.CountryId, ct); await CheckMaster(MasterKind.BusinessCategories, request.BusinessCategoryId, ct);
        shipper.Name = ServiceValidation.Required(request.CompanyName, "Company name"); shipper.LastName = ServiceValidation.Optional(request.LastName);
        shipper.EmailId = ServiceValidation.Required(request.EmailId, "Email"); shipper.Address = ServiceValidation.Required(request.Address, "Address");
        shipper.PhoneNumber = ServiceValidation.Optional(request.PhoneNumber); shipper.CountryId = request.CountryId; shipper.Logo = ServiceValidation.Optional(request.Logo);
        shipper.Status = request.Status; shipper.BusinessCategoryId = request.BusinessCategoryId;
        shipper.Consignee = reference?.Consignee; shipper.ConsigneeId = reference?.Consignee?.UniqueId;
    }
    private async Task CheckMaster(MasterKind kind, int? id, CancellationToken ct)
    {
        if (id != null && (id < 1 || await masters.GetAsync(kind, id.Value, ct) is not { IsActive: true }))
            throw new BusinessException(400, "invalid_master_reference", $"Select an active {kind} record.");
    }
    private async Task<PartyRecord> Find(PartyType type, long id, CancellationToken ct)
    { if (type == PartyType.Shipper) await shipperAccess.RequireAsync(id, ct); await access.RequireEmployeeAsync(id, ct); var reference = await repository.GetReferenceAsync(id, ct); return reference?.PartyType == type ? reference.GetParty() ?? throw Missing() : throw Missing(); }
    private static BusinessException Missing() => new(404, "party_not_found", "Linked employee, consignee or shipper was not found.");
}
