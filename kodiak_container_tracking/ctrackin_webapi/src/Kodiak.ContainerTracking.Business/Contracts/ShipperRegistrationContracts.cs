using System.ComponentModel.DataAnnotations;
namespace Kodiak.ContainerTracking.Business.Contracts;

public sealed record ShipperRegistrationRequest(
    [Required, StringLength(300)] string CompanyName,
    [Required, StringLength(300)] string ContactPerson,
    [Required, EmailAddress, StringLength(150)] string EmailId,
    [Required, Phone, StringLength(30)] string PhoneNumber,
    [Required, StringLength(2000)] string Address,
    [Range(1, int.MaxValue)] int? ConsigneeId,
    [Required, StringLength(256)] string UserName,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [Required, StringLength(128)] string ConfirmPassword,
    int? CountryId = null, int? BusinessCategoryId = null);
public sealed record ShipperRegistrationResponse(string UserName, string UniqueCode);
public sealed record RegistrationOption(int Id, string Code, string Name);
