namespace Kodiak.ContainerTracking.Business.Configuration;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Kodiak.ContainerTracking";
    public string Audience { get; set; } = "kodiak-container-tracking";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

public sealed class BootstrapOptions
{
    public bool Enabled { get; set; }
    public string UserName { get; set; } = "superadmin";
    public string FirstName { get; set; } = "Super";
    public string LastName { get; set; } = "Admin";
    public string? EmailId { get; set; }
    public string InitialPassword { get; set; } = string.Empty;
}
