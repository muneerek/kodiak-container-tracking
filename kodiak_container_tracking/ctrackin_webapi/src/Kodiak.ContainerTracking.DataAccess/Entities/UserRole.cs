namespace Kodiak.ContainerTracking.DataAccess.Entities;

public sealed class UserRole : AuditableEntity
{
    public int RoleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
    public ICollection<UserRoleMapping> UserMappings { get; set; } = [];
}
