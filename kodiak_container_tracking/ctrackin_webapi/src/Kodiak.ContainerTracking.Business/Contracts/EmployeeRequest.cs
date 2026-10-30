using System.ComponentModel.DataAnnotations;
using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record EmployeeRequest(
    [Required, StringLength(300)] string FirstName, [StringLength(300)] string? LastName,
    DateOnly? Dob, [EmailAddress, StringLength(150)] string? EmailId,
    [StringLength(2000000)] string? Logo = null,
    [EnumDataType(typeof(EmployeeStatus))] EmployeeStatus UserStatus = EmployeeStatus.Active,
    int? DesignationId = null, int? OrgId = null);
