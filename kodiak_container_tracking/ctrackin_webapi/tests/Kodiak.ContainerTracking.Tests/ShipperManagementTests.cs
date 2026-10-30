using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kodiak.ContainerTracking.Business.Contracts;
using Kodiak.ContainerTracking.DataAccess.Entities;
namespace Kodiak.ContainerTracking.Tests;

public sealed class ShipperManagementTests
{
    [Fact]
    public async Task Shippers_only_manage_own_details_and_logos_while_admin_manages_all()
    {
        var root = Path.Combine(Path.GetTempPath(), "kodiak-shipper-logo-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new ApiFactory(useJwt: true, settings: new() { ["PhotoStorage:RootPath"] = root });
            using var admin = factory.CreateApiClient(false); await factory.SeedAdminAsync();
            admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await Login(admin, "superadmin")).AccessToken);
            var registered = await admin.PostAsJsonAsync("/api/registration/shippers", new ShipperRegistrationRequest("Owner company", "Owner", "owner@example.com", "+919876543210", "Owner address", null, "shipper.owner", TestData.Password, TestData.Password)); Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
            using var owner = factory.CreateApiClient(false); var ownerLogin = await Login(owner, "shipper.owner"); owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerLogin.AccessToken);
            var ownId = ownerLogin.User.LinkedUniqueId;
            var second = await admin.PostAsJsonAsync("/api/shippers", TestData.Shipper(1) with { ConsigneeId = null }); Assert.Equal(HttpStatusCode.Created, second.StatusCode); var other = (await second.Content.ReadFromJsonAsync<PartyResponse>())!;
            var ownRoute = "/api/shippers/" + ownId; var otherRoute = "/api/shippers/" + other.UniqueId;
            Assert.Equal(2, (await admin.GetFromJsonAsync<PagedResponse<PartyResponse>>("/api/shippers"))!.TotalCount);
            var ownList = (await owner.GetFromJsonAsync<PagedResponse<PartyResponse>>("/api/shippers"))!; Assert.Equal(1, ownList.TotalCount); Assert.Equal(ownId, Assert.Single(ownList.Items).UniqueId);
            Assert.Empty((await owner.GetFromJsonAsync<PagedResponse<PartyResponse>>("/api/shippers?page=2"))!.Items);
            foreach (var path in new[] { otherRoute, otherRoute + "/logo" }) Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(path)).StatusCode);
            using (var cross = new HttpRequestMessage(HttpMethod.Put, otherRoute) { Content = JsonContent.Create(TestData.Shipper(1) with { ConsigneeId = null }) }) { cross.Headers.Add("If-Match", "\"1\""); Assert.Equal(HttpStatusCode.NotFound, (await owner.SendAsync(cross)).StatusCode); }
            Assert.Equal(HttpStatusCode.Forbidden, (await owner.DeleteAsync(ownRoute)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/shippers", TestData.Shipper(1))).StatusCode);
            var own = await owner.GetAsync(ownRoute);
            using (var status = new HttpRequestMessage(HttpMethod.Put, ownRoute) { Content = JsonContent.Create(TestData.Shipper(1) with { ConsigneeId = null, Status = BusinessStatus.Blacklisted }) }) { status.Headers.IfMatch.Add(own.Headers.ETag!); Assert.Equal(HttpStatusCode.Forbidden, (await owner.SendAsync(status)).StatusCode); }
            using (var update = new HttpRequestMessage(HttpMethod.Put, ownRoute) { Content = JsonContent.Create(TestData.Shipper(1, "Updated owner company") with { ConsigneeId = null }) }) { update.Headers.IfMatch.Add(own.Headers.ETag!); Assert.Equal(HttpStatusCode.OK, (await owner.SendAsync(update)).StatusCode); }
            own = await owner.GetAsync(ownRoute);
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
            using var upload = Upload(ownRoute + "/logo", own.Headers.ETag!, png); var uploaded = await owner.SendAsync(upload); Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode); var logo = (await uploaded.Content.ReadFromJsonAsync<PartyResponse>())!.Logo; Assert.StartsWith("shippers/", logo); Assert.True(File.Exists(Path.Combine(root, logo!)));
            Assert.Equal(png, await owner.GetByteArrayAsync(ownRoute + "/logo"));
            using (var crossUpload = Upload(otherRoute + "/logo", second.Headers.ETag!, png)) Assert.Equal(HttpStatusCode.NotFound, (await owner.SendAsync(crossUpload)).StatusCode);
            using (var crossRemove = new HttpRequestMessage(HttpMethod.Delete, otherRoute + "/logo")) { crossRemove.Headers.IfMatch.Add(second.Headers.ETag!); Assert.Equal(HttpStatusCode.NotFound, (await owner.SendAsync(crossRemove)).StatusCode); }
            using (var stale = Upload(ownRoute + "/logo", own.Headers.ETag!, png)) Assert.Equal(HttpStatusCode.PreconditionFailed, (await owner.SendAsync(stale)).StatusCode);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "shippers")));
            using (var removeLogo = new HttpRequestMessage(HttpMethod.Delete, ownRoute + "/logo")) { removeLogo.Headers.IfMatch.Add(uploaded.Headers.ETag!); Assert.Equal(HttpStatusCode.OK, (await owner.SendAsync(removeLogo)).StatusCode); }
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(ownRoute + "/logo")).StatusCode);
            var current = await admin.GetAsync(ownRoute);
            using var delete = new HttpRequestMessage(HttpMethod.Delete, ownRoute); delete.Headers.IfMatch.Add(current.Headers.ETag!); Assert.Equal(HttpStatusCode.OK, (await admin.SendAsync(delete)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync(ownRoute)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(ownRoute)).StatusCode);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static async Task<AuthenticationResponse> Login(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = name, password = TestData.Password }); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
    }
    private static HttpRequestMessage Upload(string path, EntityTagHeaderValue version, byte[] bytes)
    {
        var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(bytes), "photo", "logo.png"); var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = form }; request.Headers.IfMatch.Add(version); return request;
    }
}
