using System.ComponentModel.DataAnnotations;
namespace Kodiak.ContainerTracking.Business.Contracts;
public sealed record CreateRoleRequest([Required, StringLength(100), RegularExpression("^[A-Za-z0-9_]+$")] string Code,
    [Required, StringLength(250)] string RoleName, [StringLength(1000)] string? Description);
public sealed record UpdateRoleRequest([Required, StringLength(250)] string RoleName, [StringLength(1000)] string? Description,
    bool IsActive, [Required, StringLength(1000)] string Reason);
public sealed record ReplaceUserRolesRequest([Required, MaxLength(100)] int[] RoleIds, [Required, StringLength(1000)] string Reason);
