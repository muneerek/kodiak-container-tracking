using System.ComponentModel.DataAnnotations;
using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Business.Contracts;

public sealed record MasterRequest(
    [StringLength(50), RegularExpression("^[A-Za-z0-9_-]+$")] string? Code,
    [Required, StringLength(300)] string Name,
    bool IsActive = true, int? CountryId = null, int? StateId = null, int? CompanyId = null, int? ParentOrgId = null,
    [EmailAddress, StringLength(150)] string? EmailId = null, [StringLength(30)] string? PhoneNumber = null,
    [StringLength(2000)] string? Address = null, [StringLength(2000000)] string? Logo = null,
    [StringLength(150)] string? City = null, [StringLength(100)] string? TimeZone = null);
public sealed record MasterResponse(int Id, string Code, string Name, bool IsActive, string Version,
    int? CountryId, int? StateId, int? CompanyId, int? ParentOrgId, string? EmailId, string? PhoneNumber, string? Address, string? Logo, string? City = null, string? TimeZone = null);

internal static class MasterMapper
{
    internal static MasterResponse ToResponse(this MasterRecord record) => new(record.Id, record.Code, record.Name, record.IsActive, record.Version.ToString(),
        record switch { StateMaster state => state.CountryId, Company company => company.CountryId, ShippingPort port => port.CountryId, _ => null },
        record switch { Company company => company.StateId, ShippingPort port => port.StateId, _ => null }, (record as OrganizationHierarchy)?.CompanyId, (record as OrganizationHierarchy)?.ParentOrgId,
        (record as Company)?.EmailId, (record as Company)?.PhoneNumber, (record as Company)?.Address, (record as Company)?.Logo, (record as ShippingPort)?.City, (record as ShippingPort)?.TimeZone);
}
