using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
namespace Kodiak.ContainerTracking.Business.Services;
public interface IOverviewService { Task<OverviewResponse> GetAsync(DateOnly? date, CancellationToken ct); }
public sealed class OverviewService(IOverviewRepository repository, ICurrentActor actor, IUserManagementRepository users) : IOverviewService
{
    public async Task<OverviewResponse> GetAsync(DateOnly? date, CancellationToken ct)
    {
        var scope = new ContainerScope(true); var profile = new OverviewProfile(1,"Kodiak operations","",true);
        if (!actor.IsAdmin)
        {
            var user = actor.UserId is long id ? await users.GetUserAsync(id,ct) : null;
            if (user is not { IsActive:true }) throw Forbidden();
            if (user.UserType == UserType.Shipper && user.Reference.Shipper is { } shipper)
            {
                scope = new(false,shipper.UniqueId);
                profile = new(3,shipper.Name,shipper.UniqueCode,shipper.Status == BusinessStatus.Active,shipper.Consignee?.Name);
            }
            else if (user.UserType == UserType.Consignee && user.Reference.Consignee is { } consignee)
            {
                scope = new(false,null,consignee.UniqueId); profile = new(2,consignee.Name,consignee.UniqueCode,false);
            }
            else throw Forbidden();
        }
        var today = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await repository.ReadAsync(today,ct,scope);
        return new(result.Counts,result.Stages,result.Recent,result.Attention,result.Routes,result.Activity,today,DateTimeOffset.UtcNow,profile,result.Partners,result.PartnerCount);
    }
    private static BusinessException Forbidden() => new(403,"forbidden","An authorized container account is required.");
}
