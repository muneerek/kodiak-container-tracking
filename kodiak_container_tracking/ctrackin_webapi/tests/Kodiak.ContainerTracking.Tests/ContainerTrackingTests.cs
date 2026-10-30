using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Tests;
public sealed class ContainerTrackingTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    private static async Task<ContainerResponse> Create(HttpClient client)
    {
        async Task<int> Master(string kind, object body) { var response = await client.PostAsJsonAsync("/api/masters/" + kind, body); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<MasterResponse>())!.Id; }
        var country = await Master("Countries", new {code="T" + Guid.NewGuid().ToString("N")[..6],name="Test country"});
        var origin = await Master("ShippingPorts", new {name="Origin",countryId=country});
        var destination = await Master("ShippingPorts", new {name="Destination",countryId=country});
        var shipperResponse = await client.PostAsJsonAsync("/api/shippers", TestData.Shipper(1) with { ConsigneeId = null });
        Assert.Equal(HttpStatusCode.Created, shipperResponse.StatusCode);
        var shipper = (await shipperResponse.Content.ReadFromJsonAsync<PartyResponse>())!;
        var response = await client.PostAsJsonAsync("/api/containers", new ContainerRequest("40HC", "Cargo", 100, 10, origin, destination, new(2026,1,1), new(2026,11,10), ShipperId:shipper.UniqueId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<ContainerResponse>())!;
    }
    private static HttpRequestMessage Update(ContainerResponse item, int status, DateTimeOffset at, bool hold = false, params (string Name, byte[] Bytes)[] files)
    {
        var body = new MultipartFormDataContent();
        void Field(string name, string value) => body.Add(new StringContent(value), name);
        Field("status", status.ToString(CultureInfo.InvariantCulture)); Field("eventAt", at.ToString("O")); Field("location", "Arabian Sea");
        Field("expectedArrival", "2026-11-10"); Field("containerNumber", "MSCU 782341-6"); Field("vesselName", "MSC Aurora");
        Field("note", "Supporting documents attached"); Field("isOnHold", hold.ToString());
        foreach (var file in files) body.Add(new ByteArrayContent(file.Bytes), "files", file.Name);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/containers/" + item.Id + "/tracking") { Content = body };
        request.Headers.Add("If-Match", $"\"{item.Version}\""); return request;
    }
    [Fact]
    public async Task Tracking_filters_are_applied_before_pagination_and_options_include_only_scoped_shippers()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        var first = await Create(client); var second = await Create(client);
        using var change = Update(first,3,DateTimeOffset.UtcNow.AddMinutes(-1),true);
        Assert.Equal(HttpStatusCode.OK,(await client.SendAsync(change)).StatusCode);
        foreach (var query in new[] { "trackingStatus=3", "onHold=true&status=1", "shipperId="+first.ShipperId, "search="+first.Code })
        {
            var result=await client.GetFromJsonAsync<PagedResponse<ContainerResponse>>("/api/containers/tracking?pageSize=1&"+query);
            Assert.NotNull(result);Assert.Equal(1,result.TotalCount);Assert.Equal(first.Id,Assert.Single(result.Items).Id);
        }
        var booked=await client.GetFromJsonAsync<PagedResponse<ContainerResponse>>("/api/containers/tracking?trackingStatus=1");
        Assert.NotNull(booked);Assert.Equal(second.Id,Assert.Single(booked.Items).Id);
        var page1=await client.GetFromJsonAsync<PagedResponse<ContainerResponse>>("/api/containers/tracking?page=1&pageSize=1");
        var page2=await client.GetFromJsonAsync<PagedResponse<ContainerResponse>>("/api/containers/tracking?page=2&pageSize=1");
        Assert.NotNull(page1);Assert.NotNull(page2);Assert.Equal(2,page1.TotalCount);Assert.NotEqual(Assert.Single(page1.Items).Id,Assert.Single(page2.Items).Id);
        var choices=await client.GetFromJsonAsync<PagedResponse<Kodiak.ContainerTracking.DataAccess.Repositories.ContainerOption>>("/api/containers/options/tracking-shippers");
        Assert.NotNull(choices);Assert.Equal(2,choices.TotalCount);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/containers/tracking?trackingStatus=999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/containers/tracking?shipperId=-1")).StatusCode);
        using var anonymous=factory.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("/api/containers/tracking")).StatusCode);
    }
    [Fact]
    public async Task Tracking_saves_status_timeline_and_images_pdf_and_both_excel_formats_atomically()
    {
        var root = Path.Combine(Path.GetTempPath(), "kodiak-tracking-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new ApiFactory(settings:new() { ["ContainerStorage:RootPath"] = root }); using var client = factory.CreateApiClient();
            var item = await Create(client); var at = DateTimeOffset.UtcNow.AddMinutes(-5);
            var pdf = "%PDF-1.7\nshipping evidence"u8.ToArray();
            var xlsx = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","tracking.xlsx"));
            var xls = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","tracking.xls"));
            using var update = Update(item, 3, at, false, ("photo.png",Png), ("evidence.pdf",pdf), ("shipment.xlsx",xlsx), ("legacy.xls",xls));
            var response = await client.SendAsync(update); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = (await response.Content.ReadFromJsonAsync<ContainerResponse>())!;
            Assert.Equal(ContainerTrackingStatus.InTransit, saved.TrackingStatus); Assert.Equal("MSCU7823416", saved.ContainerNumber);
            var entry = Assert.Single(saved.TrackingEvents); Assert.Equal(4, entry.Files.Count); Assert.Equal(4, saved.Files.Count);
            Assert.All(saved.Files, file => Assert.Equal(entry.Id,file.TrackingEventId)); Assert.Equal("MSC Aurora",entry.VesselName);
            var path = "/api/containers/" + item.Id;
            foreach (var expected in new[] { ("photo.png",Png), ("evidence.pdf",pdf), ("shipment.xlsx",xlsx), ("legacy.xls",xls) })
                Assert.Equal(expected.Item2, await client.GetByteArrayAsync(path + "/files/" + saved.Files.Single(f=>f.FileName == expected.Item1).Id));
            var reloaded = (await client.GetFromJsonAsync<ContainerResponse>(path))!; Assert.Equal(4,Assert.Single(reloaded.TrackingEvents).Files.Count);
            using (var stale = Update(item,4,at.AddMinutes(1))) Assert.Equal(HttpStatusCode.PreconditionFailed,(await client.SendAsync(stale)).StatusCode);
            using (var heldDelivery = Update(saved,6,at.AddMinutes(1),true,("extra.pdf",pdf))) Assert.Equal(HttpStatusCode.Conflict,(await client.SendAsync(heldDelivery)).StatusCode);
            using (var invalidFiles = Update(saved,4,at.AddMinutes(1),false,("extra.pdf",pdf),("fake.xlsx",Png))) Assert.Equal(HttpStatusCode.BadRequest,(await client.SendAsync(invalidFiles)).StatusCode);
            Assert.Equal(4, Directory.GetFiles(Path.Combine(root,"containers")).Length);
            reloaded = (await client.GetFromJsonAsync<ContainerResponse>(path))!; Assert.Single(reloaded.TrackingEvents); Assert.Equal(saved.Version,reloaded.Version);
            using (var oldEvent = Update(saved,4,at.AddMinutes(-1))) Assert.Equal(HttpStatusCode.BadRequest,(await client.SendAsync(oldEvent)).StatusCode);
            using (var invalidStatus = Update(saved,999,at.AddMinutes(1))) Assert.Equal(HttpStatusCode.BadRequest,(await client.SendAsync(invalidStatus)).StatusCode);
            using (var hold = Update(saved,3,at.AddMinutes(1),true)) { var held = await client.SendAsync(hold); Assert.Equal(HttpStatusCode.OK,held.StatusCode); saved=(await held.Content.ReadFromJsonAsync<ContainerResponse>())!; Assert.True(saved.IsOnHold); }
            using (var delivery = Update(saved,6,at.AddMinutes(2))) { var delivered=await client.SendAsync(delivery); Assert.Equal(HttpStatusCode.OK,delivered.StatusCode); saved=(await delivered.Content.ReadFromJsonAsync<ContainerResponse>())!; Assert.False(saved.IsOnHold); Assert.Equal(3,saved.TrackingEvents.Count); }
            using var removal = new HttpRequestMessage(HttpMethod.Delete,path+"/files/"+saved.Files[0].Id); removal.Headers.Add("If-Match",$"\"{saved.Version}\"");
            Assert.Equal(HttpStatusCode.Conflict,(await client.SendAsync(removal)).StatusCode);
            using var anonymous=factory.CreateClient(); using var anonymousUpdate=Update(saved,4,at.AddMinutes(3)); Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.SendAsync(anonymousUpdate)).StatusCode);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root,true); }
    }
}
