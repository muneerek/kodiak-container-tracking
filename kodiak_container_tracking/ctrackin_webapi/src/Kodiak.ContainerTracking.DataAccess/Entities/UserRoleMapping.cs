namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class UserRoleMapping : AuditableEntity
{
    public int MappingRoleId { get; set; }
    public int FkAppUserId { get; set; }
    public int FkRoleId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public UserRole Role { get; set; } = null!;
}
