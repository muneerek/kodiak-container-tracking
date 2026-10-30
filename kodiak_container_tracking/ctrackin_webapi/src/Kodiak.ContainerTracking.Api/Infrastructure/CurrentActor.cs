using Kodiak.ContainerTracking.Business.Services;

namespace Kodiak.ContainerTracking.Api.Infrastructure;

public sealed class CurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public bool IsAdmin => accessor.HttpContext?.User.HasClaim("user_type", "1") == true && accessor.HttpContext.User.HasClaim("permission", "users.manage");
    public bool IsShipper => accessor.HttpContext?.User.HasClaim("user_type", "3") == true;
    public bool IsSuperAdmin => accessor.HttpContext?.User.IsInRole("SUPERADMIN") == true;
    public long? UserId => long.TryParse(accessor.HttpContext?.User.FindFirst("local_user_id")?.Value, out var id) && id > 0 ? id : null;
    public string Subject => accessor.HttpContext?.User.FindFirst("sub")?.Value ?? "unknown";
}
