using System.ComponentModel.DataAnnotations;
namespace Kodiak.ContainerTracking.Business.Contracts;

public sealed record LoginRequest([Required, StringLength(256)] string UserName, [Required, StringLength(128)] string Password);
public sealed record RefreshRequest([Required, StringLength(512)] string RefreshToken);
public sealed record AccessTokenResult(string AccessToken, DateTimeOffset ExpiresOn);
public sealed record AuthenticationResponse(string AccessToken, string RefreshToken, string TokenType, DateTimeOffset ExpiresOn, UserResponse User);
