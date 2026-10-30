using System.ComponentModel.DataAnnotations;
namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record CompanyProfileRequest([Required,StringLength(300)] string CompanyName,[StringLength(300)] string? LastName,
    [Required,EmailAddress,StringLength(150)] string EmailId,[Phone,StringLength(30)] string? PhoneNumber,
    [Required,StringLength(2000)] string Address,[Range(1,int.MaxValue)] int? CountryId = null);
