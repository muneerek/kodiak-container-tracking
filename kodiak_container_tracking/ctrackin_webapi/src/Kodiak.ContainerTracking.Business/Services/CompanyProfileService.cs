using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.Business.Exceptions;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Repositories;
namespace Kodiak.ContainerTracking.Business.Services;
public interface ICompanyProfileService
{
    Task<PartyResponse> GetAsync(CancellationToken ct);
    Task<PartyResponse> UpdateAsync(long version,CompanyProfileRequest request,CancellationToken ct);
}
public sealed class CompanyProfileService(IUserManagementRepository users,IPartyRepository parties,IMasterRepository masters,IUnitOfWork work,ICurrentActor actor) : ICompanyProfileService
{
    private async Task<PartyRecord> Own(CancellationToken ct)
    {
        var user=actor.UserId is long id?await users.GetUserAsync(id,ct):null;
        if(user is not {IsActive:true} || user.UserType is not (UserType.Shipper or UserType.Consignee))throw new BusinessException(403,"forbidden","A shipper or consignee account is required.");
        var reference=await parties.GetReferenceAsync(user.LinkedUniqueId,ct);
        return user.UserType switch {UserType.Shipper when reference?.Shipper is {} shipper=>shipper,UserType.Consignee when reference?.Consignee is {} consignee=>consignee,_=>throw new BusinessException(404,"profile_not_found","Your company profile was not found.")};
    }
    public async Task<PartyResponse> GetAsync(CancellationToken ct)=>(await Own(ct)).ToResponse();
    public Task<PartyResponse> UpdateAsync(long version,CompanyProfileRequest request,CancellationToken ct)
    {
        ServiceValidation.Validate(request);
        return work.InTransactionAsync(async()=>{
            var record=await Own(ct);ServiceValidation.Version(version,record.Version);
            var currentCountry=record is Shipper s?s.CountryId:((Consignee)record).CountryId;
            if(request.CountryId!=currentCountry && request.CountryId!=null && await masters.GetAsync(MasterKind.Countries,request.CountryId.Value,ct) is not {IsActive:true})
                throw new BusinessException(400,"invalid_country","Select an active country.");
            record.Name=ServiceValidation.Required(request.CompanyName,"Company name");record.EmailId=ServiceValidation.Required(request.EmailId,"Email");record.Address=ServiceValidation.Required(request.Address,"Address");
            if(record is Shipper shipper){shipper.LastName=ServiceValidation.Optional(request.LastName);shipper.PhoneNumber=ServiceValidation.Optional(request.PhoneNumber);shipper.CountryId=request.CountryId;}
            if(record is Consignee consignee){consignee.LastName=ServiceValidation.Optional(request.LastName);consignee.PhoneNumber=ServiceValidation.Optional(request.PhoneNumber);consignee.CountryId=request.CountryId;}
            record.Version++;await work.SaveAsync(actor.UserId,actor.Subject,"Update own company profile",ct);return record.ToResponse();
        },ct);
    }
}
