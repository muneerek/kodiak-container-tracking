using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Kodiak.ContainerTracking.Tests;
public sealed class PartnerOverviewTests
{
    [Fact]
    public async Task Partner_overviews_scope_every_section_and_preserve_historical_consignee_ownership()
    {
        using var factory=new ApiFactory(useJwt:true);using var admin=factory.CreateApiClient(false);await factory.SeedAdminAsync();await Login(admin,"superadmin");
        async Task<PartyResponse> Company(string kind,object body){var result=await admin.PostAsJsonAsync("/api/"+kind,body);Assert.Equal(HttpStatusCode.Created,result.StatusCode);return (await result.Content.ReadFromJsonAsync<PartyResponse>())!;}
        async Task<int> Master(string kind,object body){var result=await admin.PostAsJsonAsync("/api/masters/"+kind,body);Assert.Equal(HttpStatusCode.Created,result.StatusCode);return (await result.Content.ReadFromJsonAsync<MasterResponse>())!.Id;}
        var first=await Company("consignees",TestData.Consignee("Distributor Alpha"));var second=await Company("consignees",TestData.Consignee("Distributor Beta"));
        var role=await UserManagementApiTests.CreateRole(admin,"CONSIGNEE");
        foreach(var entry in new[]{(Name:"reader.alpha",Id:first.UniqueId),(Name:"reader.beta",Id:second.UniqueId)})
            Assert.Equal(HttpStatusCode.Created,(await admin.PostAsJsonAsync("/api/users",new{userName=entry.Name,password=TestData.Password,userType=2,linkedUniqueId=entry.Id,roleIds=new[]{role}})).StatusCode);
        async Task Register(string username,string company,int consignee) => Assert.Equal(HttpStatusCode.Created,(await admin.PostAsJsonAsync("/api/registration/shippers",new ShipperRegistrationRequest(company,username,username+"@example.com","+919876543210","Address",consignee,username,TestData.Password,TestData.Password))).StatusCode);
        await Register("owner.alpha","Alpha Exporter",first.UniqueId);await Register("owner.beta","Beta Exporter",second.UniqueId);
        var empty=await Company("shippers",TestData.Shipper(first.UniqueId,"Empty Linked Supplier"));
        var country=await Master("Countries",new{code="IN",name="India"});var originA=await Master("ShippingPorts",new{name="Alpha Origin",countryId=country});var originB=await Master("ShippingPorts",new{name="Beta Origin",countryId=country});var destination=await Master("ShippingPorts",new{name="Destination",countryId=country});
        using var ownerA=factory.CreateApiClient(false);using var ownerB=factory.CreateApiClient(false);using var readerA=factory.CreateApiClient(false);using var readerB=factory.CreateApiClient(false);
        await Login(ownerA,"owner.alpha");await Login(ownerB,"owner.beta");await Login(readerA,"reader.alpha");await Login(readerB,"reader.beta");
        async Task<ContainerResponse> RegisterContainer(HttpClient owner,int origin){var result=await owner.PostAsJsonAsync("/api/containers",new ContainerRequest("40HC","Cargo",100,10,origin,destination,new(2026,1,1),new(2026,11,10)));Assert.Equal(HttpStatusCode.Created,result.StatusCode);return (await result.Content.ReadFromJsonAsync<ContainerResponse>())!;}
        var shipmentA=await RegisterContainer(ownerA,originA);var shipmentB=await RegisterContainer(ownerB,originB);
        using(var scope=factory.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<TrackingDbContext>();var a=await db.ContainerRegistrations.SingleAsync(x=>x.Id==shipmentA.Id);var b=await db.ContainerRegistrations.SingleAsync(x=>x.Id==shipmentB.Id);
            a.TrackingStatus=ContainerTrackingStatus.InTransit;b.TrackingStatus=ContainerTrackingStatus.Loaded;b.IsOnHold=true;b.ExpectedArrival=new(2026,10,20);
            foreach(var entry in new[]{a,b})db.Set<ContainerTrackingEvent>().Add(new ContainerTrackingEvent{ContainerRegistrationId=entry.Id,Status=entry.TrackingStatus,Location=entry.Id==a.Id?"Alpha Private Location":"Beta Private Location",ExpectedArrival=entry.ExpectedArrival,IsOnHold=entry.IsOnHold,EventAt=DateTimeOffset.UtcNow.AddMinutes(-1),CreateOn=DateTimeOffset.UtcNow,UpdatedOn=DateTimeOffset.UtcNow});
            await db.SaveChangesAsync();
        }
        async Task<OverviewResponse> Overview(HttpClient client) => (await client.GetFromJsonAsync<OverviewResponse>("/api/dashboard/overview?date=2026-10-26"))!;
        var own=await Overview(ownerA);Assert.Equal(3,own.Profile.UserType);Assert.True(own.Profile.CanRegister);Assert.Equal("Alpha Exporter",own.Profile.CompanyName);Assert.Equal("Distributor Alpha",own.Profile.ConsigneeName);
        Assert.Equal(1,own.Counts.Total);Assert.Equal(1,own.Counts.InTransit);Assert.Equal(0,own.Counts.OnHold);Assert.Equal(1,own.Counts.Shippers);Assert.Equal(1,own.Counts.Consignees);Assert.Empty(own.Partners);
        Assert.All(own.Recent,x=>Assert.Equal(shipmentA.Id,x.Id));Assert.All(own.Activity,x=>Assert.Equal(shipmentA.Id,x.ContainerId));Assert.Equal("Alpha Origin",Assert.Single(own.Routes).Origin);
        var distributor=await Overview(readerA);Assert.Equal(2,distributor.Profile.UserType);Assert.False(distributor.Profile.CanRegister);Assert.Equal(1,distributor.Counts.Total);Assert.Equal(2,distributor.PartnerCount);Assert.Equal(2,distributor.Counts.Shippers);
        Assert.Equal(0,Assert.Single(distributor.Partners,x=>x.Id==empty.UniqueId).Containers);Assert.All(distributor.Activity,x=>Assert.Equal(shipmentA.Id,x.ContainerId));
        var options=await readerA.GetFromJsonAsync<PagedResponse<Kodiak.ContainerTracking.DataAccess.Repositories.ContainerOption>>("/api/containers/options/tracking-shippers");Assert.NotNull(options);Assert.Equal(2,options.TotalCount);
        var beta=await Overview(ownerB);Assert.Equal(1,beta.Counts.Total);Assert.Equal(1,beta.Counts.OnHold);Assert.Equal(1,beta.Counts.Overdue);Assert.All(beta.Activity,x=>Assert.Equal(shipmentB.Id,x.ContainerId));
        var unrelated=await Overview(readerB);Assert.Equal(1,unrelated.Counts.Total);Assert.Single(unrelated.Partners);Assert.All(unrelated.Recent,x=>Assert.Equal(shipmentB.Id,x.Id));
        var raw=await ownerA.GetStringAsync("/api/dashboard/overview?consigneeId="+second.UniqueId+"&shipperId="+shipmentB.ShipperId);Assert.DoesNotContain("Beta Private Location",raw);Assert.DoesNotContain("Beta Exporter",raw);Assert.DoesNotContain("Beta Origin",raw);
        using(var scope=factory.Services.CreateScope()) {var db=scope.ServiceProvider.GetRequiredService<TrackingDbContext>();var moving=await db.Shippers.SingleAsync(x=>x.UniqueId==shipmentA.ShipperId);moving.ConsigneeId=second.UniqueId;await db.SaveChangesAsync();}
        var historical=await Overview(readerA);Assert.Equal(1,historical.Counts.Total);Assert.Equal(shipmentA.Id,Assert.Single(historical.Recent).Id);Assert.Equal(1,historical.PartnerCount);Assert.Equal(empty.UniqueId,Assert.Single(historical.Partners).Id);
        var newNetwork=await Overview(readerB);Assert.Equal(1,newNetwork.Counts.Total);Assert.Equal(2,newNetwork.PartnerCount);Assert.Equal(0,Assert.Single(newNetwork.Partners,x=>x.Id==shipmentA.ShipperId).Containers);
        foreach(var entry in new[]{(Client:ownerA,Id:shipmentA.ShipperId,Name:"Updated Alpha"),(Client:readerA,Id:first.UniqueId,Name:"Updated Distributor")})
        {
            var profile=(await entry.Client.GetFromJsonAsync<PartyResponse>("/api/profile/company"))!;Assert.Equal(entry.Id,profile.UniqueId);
            var body=new CompanyProfileRequest(entry.Name,"Profile Contact","profile@example.com","+919876543210","Updated profile address",country);
            using var protectedFields=new HttpRequestMessage(HttpMethod.Put,"/api/profile/company"){Content=JsonContent.Create(new{companyName=entry.Name,lastName="Contact",emailId="profile@example.com",address="Address",status=2,consigneeId=first.UniqueId,uniqueId=shipmentB.ShipperId})};
            protectedFields.Headers.Add("If-Match",$"\"{profile.Version}\"");Assert.Equal(HttpStatusCode.BadRequest,(await entry.Client.SendAsync(protectedFields)).StatusCode);
            using var update=new HttpRequestMessage(HttpMethod.Put,"/api/profile/company"){Content=JsonContent.Create(body)};update.Headers.Add("If-Match",$"\"{profile.Version}\"");
            var saved=await entry.Client.SendAsync(update);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);var changed=(await saved.Content.ReadFromJsonAsync<PartyResponse>())!;
            Assert.Equal(entry.Id,changed.UniqueId);Assert.Equal(entry.Name,changed.CompanyName);Assert.Equal(BusinessStatus.Active,changed.Status);Assert.Equal(country,changed.CountryId);
            if(entry.Id==shipmentA.ShipperId)Assert.Equal(second.UniqueId,changed.ConsigneeId);
            using var stale=new HttpRequestMessage(HttpMethod.Put,"/api/profile/company"){Content=JsonContent.Create(body)};stale.Headers.Add("If-Match",$"\"{profile.Version}\"");Assert.Equal(HttpStatusCode.PreconditionFailed,(await entry.Client.SendAsync(stale)).StatusCode);
            Assert.Equal((HttpStatusCode)428,(await entry.Client.PutAsJsonAsync("/api/profile/company",body)).StatusCode);
            var ownProfile=(await entry.Client.GetFromJsonAsync<PartyResponse>("/api/profile/company?uniqueId="+shipmentB.ShipperId))!;Assert.Equal(entry.Id,ownProfile.UniqueId);
        }
        Assert.Equal(HttpStatusCode.Forbidden,(await readerA.GetAsync("/api/consignees/"+second.UniqueId)).StatusCode);
        var unchanged=(await admin.GetFromJsonAsync<PartyResponse>("/api/shippers/"+shipmentB.ShipperId))!;Assert.Equal("Beta Exporter",unchanged.CompanyName);

    }
    private static async Task Login(HttpClient client,string name){var response=await client.PostAsJsonAsync("/api/auth/login",new{userName=name,password=TestData.Password});Assert.Equal(HttpStatusCode.OK,response.StatusCode);client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",(await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);}
}
