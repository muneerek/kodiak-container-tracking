namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class UserToken : AuditableEntity
{
    public int TokenId { get; set; }
    public int FkAppUserId { get; set; }
    public string Purpose { get; set; } = "REFRESH";
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresOn { get; set; }
    public DateTimeOffset? UsedOn { get; set; }
    public DateTimeOffset? RevokedOn { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
