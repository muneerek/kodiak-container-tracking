using System.Globalization;
using Kodiak.ContainerTracking.Business.Configuration;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
using Microsoft.Extensions.Options;
namespace Kodiak.ContainerTracking.Business.Services;

public interface IShipperRegistrationService
{
    Task<ShipperRegistrationResponse> RegisterAsync(ShipperRegistrationRequest request, CancellationToken ct);
    Task<PagedResponse<RegistrationOption>> OptionsAsync(string kind, string? search, int page, int pageSize, CancellationToken ct);
}
public sealed class ShipperRegistrationService(IPartyRepository parties, IUserManagementRepository users, IMasterRepository masters,
    IRegistrationRepository selections, IUnitOfWork unitOfWork, IPasswordService passwords,
    IOptions<UniqueCodeOptions> codes, IOptions<BootstrapOptions> bootstrap) : IShipperRegistrationService
{
    public async Task<PagedResponse<RegistrationOption>> OptionsAsync(string kind, string? search, int page, int pageSize, CancellationToken ct)
    {
        ServiceValidation.Page(page, pageSize); kind = kind.ToLowerInvariant();
        if (kind is not ("consignees" or "countries" or "businesscategories") || search?.Length > 100)
            throw new BusinessException(400, "invalid_options", "Select a supported registration option and a search of 100 characters or less.");
        var (items, total) = await selections.OptionsAsync(kind, search, page, pageSize, ct);
        return new(items.Select(x => new RegistrationOption(x.Id, x.Code, x.Name)).ToArray(), total, page, pageSize);
    }
    public Task<ShipperRegistrationResponse> RegisterAsync(ShipperRegistrationRequest request, CancellationToken ct)
    {
        ServiceValidation.Validate(request);
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            throw new BusinessException(400, "password_mismatch", "Password and confirmation must match.");
        return unitOfWork.InTransactionAsync(async () =>
        {
            var name = ServiceValidation.Required(request.UserName, "Username"); var normalizedName = name.ToUpperInvariant();
            if (normalizedName == "SUPERADMIN" || string.Equals(name, bootstrap.Value.UserName, StringComparison.OrdinalIgnoreCase))
                throw new BusinessException(409, "username_unavailable", "This username is unavailable. Choose another username.");
            if (await users.UserNameExistsAsync(normalizedName, null, ct))
                throw new BusinessException(409, "username_unavailable", "This username is unavailable. Choose another username.");
            var consignee = request.ConsigneeId is int consigneeId ? (await parties.GetReferenceAsync(consigneeId, ct))?.Consignee : null;
            if (request.ConsigneeId != null && consignee is not { Status: BusinessStatus.Active }) throw new BusinessException(400, "invalid_consignee", "Select an active consignee.");
            await RequireActive(MasterKind.Countries, request.CountryId, ct); await RequireActive(MasterKind.BusinessCategories, request.BusinessCategoryId, ct);
            var role = await users.GetRolesByCodeAsync("SHIPPER", ct);
            if (role == null) { role = new UserRole { Code = "SHIPPER", NormalizedCode = "SHIPPER", RoleName = "Shipper" }; users.AddRole(role); }
            if (!role.IsActive) throw new BusinessException(409, "registration_unavailable", "Registration is currently unavailable. Contact your administrator.");
            var number = await parties.NextCodeNumberAsync(PartyType.Shipper, ct);
            var code = codes.Value.ShipperPrefix + number.ToString($"D{codes.Value.Padding}", CultureInfo.InvariantCulture);
            var shipper = new Shipper
            {
                PartyType = PartyType.Shipper, UniqueCode = code, Name = ServiceValidation.Required(request.CompanyName, "Company name"),
                LastName = ServiceValidation.Required(request.ContactPerson, "Contact person"), EmailId = ServiceValidation.Required(request.EmailId, "Email"),
                PhoneNumber = ServiceValidation.Required(request.PhoneNumber, "Phone number"), Address = ServiceValidation.Required(request.Address, "Address"),
                Consignee = consignee, ConsigneeId = consignee?.UniqueId, CountryId = request.CountryId, BusinessCategoryId = request.BusinessCategoryId,
                Status = BusinessStatus.Active, Reference = new PartyReference { PartyType = PartyType.Shipper, UniqueCode = code }
            };
            parties.Add(shipper);
            var user = new ApplicationUser { UserName = name, NormalizedUserName = normalizedName, UserType = UserType.Shipper,
                LinkedUniqueCode = code, Reference = shipper.Reference, IsActive = true };
            user.UserPassword = passwords.Hash(user, request.Password);
            user.RoleMappings.Add(new UserRoleMapping { User = user, Role = role }); users.AddUser(user);
            await unitOfWork.SaveAsync(null, "shipper-registration", "Register shipper and application user", ct);
            return new ShipperRegistrationResponse(user.UserName, code);
        }, ct);
    }
    private async Task RequireActive(MasterKind kind, int? id, CancellationToken ct)
    {
        if (id != null && (id < 1 || await masters.GetAsync(kind, id.Value, ct) is not { IsActive: true }))
            throw new BusinessException(400, "invalid_master", "Select an active country or business category.");
    }
}
