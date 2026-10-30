using System.Net;
using System.Net.Http.Json;
using Kodiak.ContainerTracking.Business.Contracts;
namespace Kodiak.ContainerTracking.Tests;

public sealed class EmployeePhotoTests
{
    // Small valid PNG fixture; no real employee photograph.
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    [Fact]
    public async Task Photos_use_configured_storage_version_checks_and_authorized_reads()
    {
        var root = Path.Combine(Path.GetTempPath(), "kodiak-photo-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new ApiFactory(settings: new() { ["PhotoStorage:RootPath"] = root }); using var client = factory.CreateApiClient();
            var created = await client.PostAsJsonAsync("/api/employees", TestData.Employee());
            var employee = (await created.Content.ReadFromJsonAsync<PartyResponse>())!;
            var route = $"/api/employees/{employee.UniqueId}/photo";
            using var request = Upload(route, Png, employee.Version);
            var uploaded = await client.SendAsync(request); Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
            var updated = (await uploaded.Content.ReadFromJsonAsync<PartyResponse>())!;
            Assert.StartsWith("employees/", updated.Logo); Assert.DoesNotContain(root, updated.Logo!);
            Assert.True(File.Exists(Path.Combine(root, updated.Logo!)));
            var read = await client.GetAsync(route); Assert.Equal("image/png", read.Content.Headers.ContentType!.MediaType); Assert.Equal(Png, await read.Content.ReadAsByteArrayAsync());
            using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(route)).StatusCode);
            using var stale = Upload(route, Png, employee.Version); Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "employees")));
            using var invalid = Upload(route, "<svg>invalid</svg>"u8.ToArray(), updated.Version); Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(invalid)).StatusCode);
            using var remove = new HttpRequestMessage(HttpMethod.Delete, route); remove.Headers.Add("If-Match", $"\"{updated.Version}\"");
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(remove)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/employees", TestData.Employee() with { Logo = updated.Logo })).StatusCode);
            var traversal = await client.PostAsJsonAsync("/api/employees", TestData.Employee() with { Logo = "../../appsettings.json" });
            var other = (await traversal.Content.ReadFromJsonAsync<PartyResponse>())!;
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/employees/{other.UniqueId}/photo")).StatusCode);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static HttpRequestMessage Upload(string route, byte[] bytes, string version)
    {
        var content = new MultipartFormDataContent(); content.Add(new ByteArrayContent(bytes), "photo", "untrusted-name.png");
        var request = new HttpRequestMessage(HttpMethod.Put, route) { Content = content }; request.Headers.Add("If-Match", $"\"{version}\""); return request;
    }
}
