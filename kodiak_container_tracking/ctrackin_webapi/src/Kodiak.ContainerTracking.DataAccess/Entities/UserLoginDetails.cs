namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class UserLoginDetails
{
    public int LoginId { get; set; }
    public int FkAppUserId { get; set; }
    public DateTimeOffset LoginTime { get; set; }
    public DateTimeOffset? LogoutTime { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
