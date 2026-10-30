using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Business.Services;

public interface IPasswordService
{
    string Hash(ApplicationUser user, string password);
    bool Verify(ApplicationUser? user, string password);
}
