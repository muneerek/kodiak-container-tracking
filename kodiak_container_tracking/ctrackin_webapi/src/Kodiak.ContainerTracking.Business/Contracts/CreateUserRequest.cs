using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record CreateUserRequest(
    [Required, StringLength(256)] string UserName,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [EnumDataType(typeof(UserType))] UserType UserType,
    [Range(1, int.MaxValue)] [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] int LinkedUniqueId,
    [Required, MinLength(1), MaxLength(100)] int[] RoleIds, bool IsActive = true);
