using System.ComponentModel.DataAnnotations;
using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record CompanyRequest(
    [Required, StringLength(300)] string CompanyName, [StringLength(300)] string? LastName,
    [Required, EmailAddress, StringLength(150)] string EmailId, [Phone, StringLength(30)] string? PhoneNumber,
    [Required, StringLength(2000)] string Address, int? CountryId = null,
    [StringLength(2000000)] string? Logo = null,
    [EnumDataType(typeof(BusinessStatus))] BusinessStatus Status = BusinessStatus.Active);
