using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record ConsigneeSelection(int ConsigneeId, string UniqueCode, string CompanyName);
public sealed record PartyResponse(int UniqueId, string UniqueCode, PartyType PartyType, string Name,
    string? FirstName, string? LastName, DateOnly? Dob, string? EmailId, string? PhoneNumber, string? Address, string? Logo,
    EmployeeStatus? UserStatus, BusinessStatus? Status, int? DesignationId, int? OrgId, int? CountryId, int? BusinessCategoryId,
    int? ConsigneeId, ConsigneeSelection? Consignee, string Version)
{
    public string? CompanyName => PartyType is PartyType.Consignee or PartyType.Shipper ? Name : null;
}
internal static class PartyResponseMapper
{
    internal static PartyRecord? GetParty(this PartyReference reference) => reference.PartyType switch
    { PartyType.Employee => reference.Employee, PartyType.Consignee => reference.Consignee, PartyType.Shipper => reference.Shipper, _ => null };
    internal static PartyResponse ToResponse(this PartyRecord party) => new(party.UniqueId, party.UniqueCode, party.PartyType,
        party is Employee e ? string.Join(" ", new[] { e.FirstName, e.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))) : party.Name,
        (party as Employee)?.FirstName, party switch { Employee employee => employee.LastName, Consignee c => c.LastName, Shipper s => s.LastName, _ => null },
        (party as Employee)?.Dob, party.EmailId, party switch { Consignee c => c.PhoneNumber, Shipper s => s.PhoneNumber, _ => null }, party.Address, party.Logo,
        (party as Employee)?.UserStatus, party switch { Consignee c => c.Status, Shipper s => s.Status, _ => null },
        (party as Employee)?.DesignationId, (party as Employee)?.OrgId, party switch { Consignee c => c.CountryId, Shipper s => s.CountryId, _ => null },
        (party as Shipper)?.BusinessCategoryId, (party as Shipper)?.ConsigneeId,
        party is Shipper { Consignee: not null } shipper ? new(shipper.Consignee.UniqueId, shipper.Consignee.UniqueCode, shipper.Consignee.Name) : null,
        party.Version.ToString());
}
