using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Kodiak.ContainerTracking.Business.Configuration;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
using Microsoft.Extensions.Options;
namespace Kodiak.ContainerTracking.Business.Services;

public interface IBootstrapService { Task<int> EnsureSuperAdminAsync(CancellationToken ct); }

public sealed class BootstrapService(IMasterRepository masters, IPartyRepository parties, IUserManagementRepository users,
    IUnitOfWork unitOfWork, IPasswordService passwords, IOptions<BootstrapOptions> bootstrap, IOptions<UniqueCodeOptions> codeOptions) : IBootstrapService
{
    public Task<int> EnsureSuperAdminAsync(CancellationToken ct) => unitOfWork.InTransactionAsync(async () =>
    {
        var configuration = bootstrap.Value;
        var name = ServiceValidation.Required(configuration.UserName, "Bootstrap username");
        if (name.Length > 256) throw new BusinessException(400, "invalid_bootstrap", "Bootstrap username is too long.");
        var existing = await users.GetByNameAsync(name.ToUpperInvariant(), ct);
        if (existing != null)
        {
            if (existing.UserType != UserType.Admin || !AuthenticationService.IsEligible(existing)
                || !existing.RoleMappings.Any(x => x.Role.Code == "SUPERADMIN" && x.Role.IsActive))
                throw new BusinessException(409, "bootstrap_conflict", "Bootstrap username already belongs to a different or inactive account. No existing account was modified.");
            return existing.AppUserId;
        }
        if (string.IsNullOrWhiteSpace(configuration.InitialPassword) || configuration.InitialPassword.Length is < 12 or > 128)
            throw new BusinessException(400, "bootstrap_password_required", "Set Bootstrap:InitialPassword to a password of 12–128 characters.");
        if (configuration.EmailId != null && !new EmailAddressAttribute().IsValid(configuration.EmailId))
            throw new BusinessException(400, "bootstrap_email", "Bootstrap email is invalid.");
        var company = (Company?)await masters.GetByCodeAsync(MasterKind.Companies, "KODIAK", ct);
        if (company == null) { company = new Company { Code = "KODIAK", Name = "Kodiak" }; masters.Add(company); }
        var designation = (Designation?)await masters.GetByCodeAsync(MasterKind.Designations, "SUPERADMIN", ct);
        if (designation == null) { designation = new Designation { Code = "SUPERADMIN", Name = "Super Administrator" }; masters.Add(designation); }
        var org = (OrganizationHierarchy?)await masters.GetByCodeAsync(MasterKind.Organizations, "HEAD_OFFICE", ct);
        if (org == null) { org = new OrganizationHierarchy { Code = "HEAD_OFFICE", Name = "Head Office", Company = company }; masters.Add(org); }
        if (!company.IsActive || !designation.IsActive || !org.IsActive) throw new BusinessException(409, "bootstrap_master_inactive", "Bootstrap master records must be active.");
        if (company.Id != 0 && org.CompanyId != 0 && org.CompanyId != company.Id)
            throw new BusinessException(409, "bootstrap_master_conflict", "HEAD_OFFICE belongs to a different company.");
        if (configuration.FirstName.Length > 300 || configuration.LastName.Length > 300 || configuration.EmailId?.Length > 150)
            throw new BusinessException(400, "invalid_bootstrap", "Bootstrap employee details exceed the supported lengths.");
        var code = codeOptions.Value.EmployeePrefix + (await parties.NextCodeNumberAsync(PartyType.Employee, ct)).ToString($"D{codeOptions.Value.Padding}", CultureInfo.InvariantCulture);
        var employee = new Employee { FirstName = ServiceValidation.Required(configuration.FirstName, "First name"), LastName = ServiceValidation.Required(configuration.LastName, "Last name"),
            EmailId = configuration.EmailId, UserStatus = EmployeeStatus.Active, Designation = designation, Organization = org,
            PartyType = PartyType.Employee, UniqueCode = code, Reference = new PartyReference { PartyType = PartyType.Employee, UniqueCode = code } };
        parties.Add(employee);
        var role = await users.GetRolesByCodeAsync("SUPERADMIN", ct);
        if (role == null) { role = new UserRole { Code = "SUPERADMIN", NormalizedCode = "SUPERADMIN", RoleName = "Super Administrator" }; users.AddRole(role); }
        if (!role.IsActive) throw new BusinessException(409, "bootstrap_role_inactive", "The SUPERADMIN role is inactive.");
        var user = new ApplicationUser { UserName = name, NormalizedUserName = name.ToUpperInvariant(), UserType = UserType.Admin,
            LinkedUniqueCode = code, Reference = employee.Reference, IsActive = true };
        user.UserPassword = passwords.Hash(user, configuration.InitialPassword);
        user.RoleMappings.Add(new UserRoleMapping { User = user, Role = role });
        users.AddUser(user);
        await unitOfWork.SaveAsync(null, "bootstrap", "Create super-admin employee and application user", ct);
        return user.AppUserId;
    }, ct);
}
