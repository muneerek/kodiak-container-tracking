namespace Kodiak.ContainerTracking.Business.Services;

public interface ICurrentActor
{
    long? UserId { get; }
    string Subject { get; }
    bool IsSuperAdmin { get; }
    bool IsAdmin => IsSuperAdmin;
    bool IsShipper => false;
}
