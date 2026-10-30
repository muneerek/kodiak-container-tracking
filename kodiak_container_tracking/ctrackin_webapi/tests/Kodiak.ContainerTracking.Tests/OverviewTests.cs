using System.Net;
using System.Net.Http.Json;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Kodiak.ContainerTracking.Tests;
public sealed class OverviewTests
{
    [Fact]
    public async Task Empty_overview_has_zero_counts_and_requires_admin_access()
    {
        using var factory=new ApiFactory();using var client=factory.CreateApiClient();
        var result=await client.GetFromJsonAsync<OverviewResponse>("/api/dashboard/overview?date=2026-10-26");
        Assert.NotNull(result);Assert.Equal(0,result.Counts.Total);Assert.Equal(0,result.Counts.Routes);Assert.Equal(0,result.Counts.Countries);
        Assert.Empty(result.Stages);Assert.Empty(result.Recent);Assert.Empty(result.Attention);Assert.Empty(result.Activity);Assert.Equal(new DateOnly(2026,10,26),result.BusinessDate);
        using var anonymous=factory.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("/api/dashboard/overview")).StatusCode);
        using var denied=factory.CreateApiClient(false);denied.DefaultRequestHeaders.Add("X-Test-Access","reader");Assert.Equal(HttpStatusCode.Forbidden,(await denied.GetAsync("/api/dashboard/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/dashboard/overview?date=not-a-date")).StatusCode);
    }
    [Fact]
    public async Task Overview_counts_reconcile_and_attention_excludes_delivered_and_cancelled_shipments()
    {
        using var factory=new ApiFactory();using var client=factory.CreateApiClient();
        async Task<int> Master(string kind,object body){var response=await client.PostAsJsonAsync("/api/masters/"+kind,body);Assert.Equal(HttpStatusCode.Created,response.StatusCode);return (await response.Content.ReadFromJsonAsync<MasterResponse>())!.Id;}
        var country=await Master("Countries",new{code="IN",name="India"});
        var origin=await Master("ShippingPorts",new{name="Mumbai",countryId=country});var destination=await Master("ShippingPorts",new{name="Kochi",countryId=country});
        var consigneeResponse=await client.PostAsJsonAsync("/api/consignees",TestData.Consignee());Assert.Equal(HttpStatusCode.Created,consigneeResponse.StatusCode);var consignee=(await consigneeResponse.Content.ReadFromJsonAsync<PartyResponse>())!;
        var shipperResponse=await client.PostAsJsonAsync("/api/shippers",TestData.Shipper(consignee.UniqueId));Assert.Equal(HttpStatusCode.Created,shipperResponse.StatusCode);var shipper=(await shipperResponse.Content.ReadFromJsonAsync<PartyResponse>())!;
        var ids=new List<int>();for(var i=0;i<6;i++){var response=await client.PostAsJsonAsync("/api/containers",new ContainerRequest("40HC","Cargo",100,10,origin,destination,new(2026,1,1),new(2026,11,10),ShipperId:shipper.UniqueId));Assert.Equal(HttpStatusCode.Created,response.StatusCode);ids.Add((await response.Content.ReadFromJsonAsync<ContainerResponse>())!.Id);}
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<TrackingDbContext>();var items=await db.ContainerRegistrations.OrderBy(x=>x.Id).ToListAsync();
            foreach(var item in items)item.ContainerNumber="TEST"+item.Id.ToString("D7");
            items[0].TrackingStatus=ContainerTrackingStatus.InTransit;
            items[1].TrackingStatus=ContainerTrackingStatus.InTransit;items[1].IsOnHold=true;items[1].ExpectedArrival=new(2026,10,20);
            items[2].TrackingStatus=ContainerTrackingStatus.Delivered;items[2].ExpectedArrival=new(2026,10,20);
            items[3].ContainerNumber=null;
            items[4].Status=RegistrationStatus.Cancelled;items[4].IsOnHold=true;items[4].ContainerNumber=null;items[4].ExpectedArrival=new(2026,10,20);
            items[5].TrackingStatus=ContainerTrackingStatus.Loaded;items[5].ExpectedArrival=new(2026,10,20);
            db.Set<ContainerTrackingEvent>().Add(new ContainerTrackingEvent{ContainerRegistrationId=items[0].Id,Status=ContainerTrackingStatus.InTransit,Location="Arabian Sea",ExpectedArrival=items[0].ExpectedArrival,EventAt=DateTimeOffset.UtcNow.AddMinutes(-1),CreateOn=DateTimeOffset.UtcNow,UpdatedOn=DateTimeOffset.UtcNow});
            await db.SaveChangesAsync();
        }
        var result=await client.GetFromJsonAsync<OverviewResponse>("/api/dashboard/overview?date=2026-10-26");Assert.NotNull(result);
        Assert.Equal(6,result.Counts.Total);Assert.Equal(5,result.Counts.Active);Assert.Equal(1,result.Counts.Cancelled);Assert.Equal(2,result.Counts.InTransit);Assert.Equal(1,result.Counts.Delivered);
        Assert.Equal(1,result.Counts.OnHold);Assert.Equal(2,result.Counts.Overdue);Assert.Equal(3,result.Counts.Attention);Assert.Equal(1,result.Counts.Shippers);Assert.Equal(1,result.Counts.Consignees);Assert.Equal(1,result.Counts.Routes);Assert.Equal(1,result.Counts.Countries);
        Assert.Equal(6,result.Stages.Sum(x=>x.Count));Assert.Equal(1,Assert.Single(result.Stages,x=>x.Key=="hold").Count);
        Assert.Equal(5,result.Recent.Count);Assert.Equal(3,result.Attention.Count);Assert.Equal(ids[1],result.Attention[0].Id);Assert.DoesNotContain(result.Attention,x=>x.Id==ids[2]||x.Id==ids[4]);
        Assert.Equal(5,Assert.Single(result.Routes).Containers);Assert.Equal(6,result.Activity.Count);Assert.Equal("tracking",result.Activity[0].Kind);Assert.Equal(ids[0],result.Activity[0].ContainerId);
    }
}
