using System.ComponentModel.DataAnnotations;
namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record UpdateUserRequest([Required, StringLength(256)] string UserName, bool IsActive = true);
public sealed record SetPasswordRequest([Required, StringLength(128, MinimumLength = 12)] string Password,
    [Required, StringLength(1000)] string Reason);
