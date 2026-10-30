namespace Kodiak.ContainerTracking.DataAccess.Entities;

public enum UserType { Normal = 0, Admin = 1, Consignee = 2, Shipper = 3 }
public enum PartyType { Employee = 1, Consignee = 2, Shipper = 3 }
public enum EmployeeStatus { Active = 1, Terminated = 2 }
public enum BusinessStatus { Active = 1, Blacklisted = 2 }

public sealed class ApplicationUser : AuditableEntity
{
    public int AppUserId { get; set; }
    public UserType UserType { get; set; }
    public int LinkedUniqueId { get; set; }
    public string LinkedUniqueCode { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string? UserPassword { get; set; }
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
    public PartyReference Reference { get; set; } = null!;
    public ICollection<UserRoleMapping> RoleMappings { get; set; } = [];
}
