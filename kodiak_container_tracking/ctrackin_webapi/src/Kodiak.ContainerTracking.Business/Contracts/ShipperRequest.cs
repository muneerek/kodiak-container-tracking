using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record ShipperRequest(
    [Required, StringLength(300)] string CompanyName, [StringLength(300)] string? LastName,
    [Required, EmailAddress, StringLength(150)] string EmailId, [Phone, StringLength(30)] string? PhoneNumber,
    [Required, StringLength(2000)] string Address,
    [Range(1, int.MaxValue)] [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] int? ConsigneeId,
    int? CountryId = null, int? BusinessCategoryId = null, [StringLength(2000000)] string? Logo = null,
    [EnumDataType(typeof(BusinessStatus))] BusinessStatus Status = BusinessStatus.Active);
