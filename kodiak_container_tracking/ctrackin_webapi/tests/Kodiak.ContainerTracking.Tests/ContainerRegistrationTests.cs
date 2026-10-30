using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Kodiak.ContainerTracking.Tests;
public sealed class ContainerRegistrationTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    private static ContainerRequest Input(int origin, int destination, int? shipper = null) => new("40HC", "Machine components", 1200.5m, 20, origin, destination, new(2026, 11, 1), new(2026, 11, 10), null, "PO-123", shipper);
    private static async Task<int> Master(HttpClient client, string kind, object body) { var r = await client.PostAsJsonAsync("/api/masters/" + kind, body); Assert.Equal(HttpStatusCode.Created, r.StatusCode); return (await r.Content.ReadFromJsonAsync<MasterResponse>())!.Id; }
    private static async Task<(int Origin, int Destination)> Ports(HttpClient client)
    {
        var country = await Master(client, "Countries", new { code = "IN", name = "India" });
        var first = await Master(client, "ShippingPorts", new { code = "INNSA", name = "Nhava Sheva", countryId = country });
        var second = await Master(client, "ShippingPorts", new { code = "INMAA", name = "Chennai", countryId = country }); return (first, second);
    }
    [Fact]
    public async Task Registration_validates_fields_duplicate_physical_numbers_versions_and_cancellation()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient(); var ports = await Ports(client);
        var shipperResponse = await client.PostAsJsonAsync("/api/shippers", TestData.Shipper(1) with { ConsigneeId = null }); var shipper = (await shipperResponse.Content.ReadFromJsonAsync<PartyResponse>())!;
        var input = Input(ports.Origin, ports.Destination, shipper.UniqueId);
        foreach (var invalid in new[] { input with { DestinationPortId = ports.Origin }, input with { ExpectedArrival = new(2026, 10, 1) }, input with { WeightKg = 0 }, input with { Packages = 0 }, input with { ContainerType = "unknown" }, input with { ContainerNumber = "invalid" } }) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/containers", invalid)).StatusCode);
        var created = await client.PostAsJsonAsync("/api/containers", input with { ContainerNumber = "MSCU 782341-6" }); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = (await created.Content.ReadFromJsonAsync<ContainerResponse>())!; Assert.Equal("KD-000001", item.Code); Assert.Equal("MSCU7823416", item.ContainerNumber); Assert.Equal(RegistrationStatus.Booked, item.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/containers", input with { ContainerNumber = "MSCU7823416" })).StatusCode);
        using var update = new HttpRequestMessage(HttpMethod.Put, "/api/containers/" + item.Id) { Content = JsonContent.Create(input with { Cargo = "Updated cargo", ContainerNumber = "MSCU7823416" }) }; update.Headers.IfMatch.Add(created.Headers.ETag!); var updated = await client.SendAsync(update); Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var stale = new HttpRequestMessage(HttpMethod.Delete, "/api/containers/" + item.Id); stale.Headers.IfMatch.Add(created.Headers.ETag!); Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
        using var cancel = new HttpRequestMessage(HttpMethod.Delete, "/api/containers/" + item.Id); cancel.Headers.IfMatch.Add(updated.Headers.ETag!); Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/containers", input with { ContainerNumber = "MSCU7823416" })).StatusCode);
        foreach (var term in new[] { "chennai", "PORT-000001", "PO-123", "KD-000001" })
        {
            var filtered = await client.GetFromJsonAsync<PagedResponse<ContainerResponse>>("/api/containers?status=2&search=" + term);
            Assert.Single(filtered!.Items); Assert.Equal(1, filtered.TotalCount); Assert.Equal(item.Id, filtered.Items[0].Id);
        }
        var booked = await client.GetFromJsonAsync<PagedResponse<ContainerResponse>>("/api/containers?status=1&search=PORT-000002");
        Assert.Single(booked!.Items); Assert.NotEqual(item.Id, booked.Items[0].Id);
        Assert.Empty((await client.GetFromJsonAsync<PagedResponse<ContainerResponse>>("/api/containers?status=2&search=unknown"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/containers?status=999")).StatusCode);
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/containers")).StatusCode);
    }
    [Fact]
    public async Task Owners_consignee_read_scope_and_files_are_authorized_and_bounded()
    {
        var root = Path.Combine(Path.GetTempPath(), "kodiak-container-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new ApiFactory(useJwt: true, settings: new() { ["ContainerStorage:RootPath"] = root, ["ContainerStorage:MaxFileBytes"] = "65536", ["ContainerStorage:MaxFilesPerContainer"] = "2" });
            using var admin = factory.CreateApiClient(false); await factory.SeedAdminAsync(); await Login(admin, "superadmin"); var ports = await Ports(admin);
            var consigneeId = await UserManagementApiTests.CreateMaster(admin, 2); var consigneeRole = await UserManagementApiTests.CreateRole(admin, "CONSIGNEE");
            Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/users", new { userName = "consignee.reader", password = TestData.Password, userType = 2, linkedUniqueId = consigneeId, roleIds = new[] { consigneeRole } })).StatusCode);
            using var owner = factory.CreateApiClient(false); using var other = factory.CreateApiClient(false); using var consignee = factory.CreateApiClient(false);
            await Register(admin, "owner", consigneeId); await Register(admin, "other", null);
            await Login(owner, "owner"); await Login(other, "other"); await Login(consignee, "consignee.reader");
            var created = await owner.PostAsJsonAsync("/api/containers", Input(ports.Origin, ports.Destination)); Assert.Equal(HttpStatusCode.Created, created.StatusCode); var item = (await created.Content.ReadFromJsonAsync<ContainerResponse>())!;
            var path = "/api/containers/" + item.Id; Assert.Equal(consigneeId, item.ConsigneeId);
            Assert.Empty((await other.GetFromJsonAsync<PagedResponse<ContainerResponse>>("/api/containers?status=1&search=INNSA"))!.Items); Assert.Single((await consignee.GetFromJsonAsync<PagedResponse<ContainerResponse>>("/api/containers"))!.Items);
            Assert.Empty((await other.GetFromJsonAsync<PagedResponse<ContainerResponse>>("/api/containers/tracking?shipperId=" + item.ShipperId))!.Items);
            Assert.Single((await consignee.GetFromJsonAsync<PagedResponse<ContainerResponse>>("/api/containers/tracking"))!.Items);
            Assert.Empty((await other.GetFromJsonAsync<PagedResponse<Kodiak.ContainerTracking.DataAccess.Repositories.ContainerOption>>("/api/containers/options/tracking-shippers"))!.Items);
            Assert.Single((await consignee.GetFromJsonAsync<PagedResponse<Kodiak.ContainerTracking.DataAccess.Repositories.ContainerOption>>("/api/containers/options/tracking-shippers"))!.Items);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await owner.DeleteAsync(path)).StatusCode);
            using (var denied = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(Input(ports.Origin, ports.Destination)) }) { denied.Headers.IfMatch.Add(created.Headers.ETag!); Assert.Equal(HttpStatusCode.Forbidden, (await consignee.SendAsync(denied)).StatusCode); }
            using (var deniedTracking = new MultipartFormDataContent()) Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsync(path + "/tracking", deniedTracking)).StatusCode);
            using (var deniedTracking = new MultipartFormDataContent()) Assert.Equal(HttpStatusCode.Forbidden, (await consignee.PostAsync(path + "/tracking", deniedTracking)).StatusCode);
            using var photoUpload = Upload(path, item.Version, "photo.png", Png, 1); var photoResponse = await owner.SendAsync(photoUpload); Assert.Equal(HttpStatusCode.OK, photoResponse.StatusCode); item = (await photoResponse.Content.ReadFromJsonAsync<ContainerResponse>())!; var photo = Assert.Single(item.Files);
            Assert.Equal(Png, await consignee.GetByteArrayAsync(path + "/files/" + photo.Id)); Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + "/files/" + photo.Id)).StatusCode);
            using (var deniedUpload = Upload(path, item.Version, "photo.png", Png, 1)) Assert.Equal(HttpStatusCode.Forbidden, (await consignee.SendAsync(deniedUpload)).StatusCode);
            using var documentUpload = Upload(path, item.Version, "evidence.pdf", "%PDF-1.7\nreference document"u8.ToArray(), 2); var documentResponse = await owner.SendAsync(documentUpload); Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode); item = (await documentResponse.Content.ReadFromJsonAsync<ContainerResponse>())!; Assert.Equal(2, item.Files.Count);
            using (var invalid = Upload(path, item.Version, "photo.png", "<html>invalid</html>"u8.ToArray(), 1)) Assert.Equal(HttpStatusCode.BadRequest, (await owner.SendAsync(invalid)).StatusCode);
            using (var large = Upload(path, item.Version, "large.txt", Encoding.UTF8.GetBytes(new string('a', 70000)), 2)) Assert.Equal(HttpStatusCode.BadRequest, (await owner.SendAsync(large)).StatusCode);
            using (var overLimit = Upload(path, item.Version, "more.txt", "reference"u8.ToArray(), 2)) Assert.Equal(HttpStatusCode.BadRequest, (await owner.SendAsync(overLimit)).StatusCode);
            Assert.Equal(2, Directory.GetFiles(Path.Combine(root, "containers")).Length);
            using (var stale = Upload(path, "1", "photo.png", Png, 1)) Assert.Equal(HttpStatusCode.PreconditionFailed, (await owner.SendAsync(stale)).StatusCode);
            using var removal = new HttpRequestMessage(HttpMethod.Delete, path + "/files/" + photo.Id); removal.Headers.Add("If-Match", $"\"{item.Version}\""); var removed = await owner.SendAsync(removal); Assert.Equal(HttpStatusCode.OK, removed.StatusCode); Assert.Single((await removed.Content.ReadFromJsonAsync<ContainerResponse>())!.Files);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(path + "/files/" + photo.Id)).StatusCode);
            Assert.DoesNotContain("relativePath", await removed.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static async Task Register(HttpClient client, string name, int? consignee)
    { Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/registration/shippers", new ShipperRegistrationRequest(name + " company", name, name + "@example.com", "+919876543210", "Address", consignee, name, TestData.Password, TestData.Password))).StatusCode); }
    private static async Task Login(HttpClient client, string name)
    { var result = await client.PostAsJsonAsync("/api/auth/login", new { userName = name, password = TestData.Password }); Assert.Equal(HttpStatusCode.OK, result.StatusCode); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await result.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken); }
    private static HttpRequestMessage Upload(string path, string version, string name, byte[] bytes, int kind)
    { var body = new MultipartFormDataContent(); body.Add(new ByteArrayContent(bytes), "file", name); body.Add(new StringContent(kind.ToString()), "kind"); var request = new HttpRequestMessage(HttpMethod.Post, path + "/files") { Content = body }; request.Headers.Add("If-Match", $"\"{version}\""); return request; }
}
