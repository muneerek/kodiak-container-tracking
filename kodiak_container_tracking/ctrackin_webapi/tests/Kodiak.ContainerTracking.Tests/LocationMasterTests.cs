using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Kodiak.ContainerTracking.Business.Contracts;
namespace Kodiak.ContainerTracking.Tests;

public sealed class LocationMasterTests
{
    [Fact]
    public async Task Port_crud_validates_country_state_and_preserves_references()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateApiClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
            db.TimeZones.Add(new TimeZoneMaster { Name = "Asia/Kolkata", Abbreviation = "IST", UtcOffsetSeconds = 19800, ObservedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var zones = await client.GetFromJsonAsync<TimeZoneResponse[]>("/api/masters/timezones");
        Assert.NotNull(zones);
        Assert.Equal(new[] { "Asia/Kolkata", "UTC" }, zones.Select(x => x.Name));
        Assert.Equal(19800, zones[0].UtcOffsetSeconds);
        async Task<MasterResponse> Create(string kind, object body) { var result = await client.PostAsJsonAsync("/api/masters/" + kind, body); Assert.Equal(HttpStatusCode.Created, result.StatusCode); return (await result.Content.ReadFromJsonAsync<MasterResponse>())!; }
        var india = await Create("Countries", new { code = "IN", name = "India" }); var uae = await Create("Countries", new { code = "AE", name = "United Arab Emirates" });
        var state = await Create("States", new { code = "IN-MH", name = "Maharashtra", countryId = india.Id });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/masters/ShippingPorts", new { code = "BAD", name = "Invalid port", countryId = uae.Id, stateId = state.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/masters/ShippingPorts", new { code = "BAD", name = "Invalid zone", countryId = india.Id, timeZone = "Not/AZone" })).StatusCode);
        var preview = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/masters/ShippingPorts/next-code");
        Assert.Equal("PORT-000001", preview.GetProperty("code").GetString());
        var repeatedPreview = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/masters/ShippingPorts/next-code");
        Assert.Equal("PORT-000001", repeatedPreview.GetProperty("code").GetString());
        var port = await Create("ShippingPorts", new { code = "INNSA", name = "Nhava Sheva", countryId = india.Id, stateId = state.Id, city = "Navi Mumbai", timeZone = "Asia/Kolkata" });
        Assert.Equal("PORT-000001", port.Code);
        var secondPort = await Create("ShippingPorts", new { name = "Dubai port", countryId = uae.Id });
        Assert.Equal("PORT-000002", secondPort.Code);
        var nextPreview = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/masters/ShippingPorts/next-code");
        Assert.Equal("PORT-000003", nextPreview.GetProperty("code").GetString());
        Assert.Equal("UTC", secondPort.TimeZone);
        var path = "/api/masters/ShippingPorts/" + port.Id;
        Assert.Equal(state.Id, (await client.GetFromJsonAsync<MasterResponse>(path))!.StateId);
        Assert.Equal((HttpStatusCode)428, (await client.DeleteAsync(path)).StatusCode);
        using var deletion = new HttpRequestMessage(HttpMethod.Delete, path); deletion.Headers.Add("If-Match", $"\"{port.Version}\""); var deleted = await client.SendAsync(deletion); Assert.Equal(HttpStatusCode.OK, deleted.StatusCode); Assert.False((await deleted.Content.ReadFromJsonAsync<MasterResponse>())!.IsActive);
        Assert.Equal("Asia/Kolkata", (await client.GetFromJsonAsync<MasterResponse>(path))!.TimeZone);
        using var stale = new HttpRequestMessage(HttpMethod.Delete, path); stale.Headers.Add("If-Match", $"\"{port.Version}\""); Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
        using var update = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(new { code = "INNSA", name = "Updated port", countryId = india.Id, stateId = state.Id, isActive = true }) }; update.Headers.IfMatch.Add(deleted.Headers.ETag!); Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(update)).StatusCode);
        var updatedPort = (await client.GetFromJsonAsync<MasterResponse>(path))!;
        Assert.Equal(port.Code, updatedPort.Code);
        Assert.Equal("Asia/Kolkata", updatedPort.TimeZone);
        using var moveState = new HttpRequestMessage(HttpMethod.Put, "/api/masters/States/" + state.Id) { Content = JsonContent.Create(new { code = state.Code, name = state.Name, countryId = uae.Id }) }; moveState.Headers.Add("If-Match", $"\"{state.Version}\""); Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(moveState)).StatusCode);
        foreach (var item in new[] { ("States", state), ("Countries", india) }) { using var deactivate = new HttpRequestMessage(HttpMethod.Delete, "/api/masters/" + item.Item1 + "/" + item.Item2.Id); deactivate.Headers.Add("If-Match", $"\"{item.Item2.Version}\""); Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(deactivate)).StatusCode); }
        Assert.Equal(india.Id, (await client.GetFromJsonAsync<MasterResponse>(path))!.CountryId);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/masters/timezones")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/masters/ShippingPorts")).StatusCode);
    }
}
