using System.Net;
namespace Kodiak.ContainerTracking.Tests;
public sealed class DeploymentHostingTests
{
    [Fact]
    public async Task Serves_vue_and_spa_routes_but_never_returns_html_for_unknown_api_routes()
    {
        var root=Path.Combine(Path.GetTempPath(),"kodiak-static-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            await File.WriteAllTextAsync(Path.Combine(root,"index.html"),"<html>Kodiak deployment test</html>");
            await File.WriteAllTextAsync(Path.Combine(root,"app.js"),"window.kodiak=true;");
            using var factory=new ApiFactory(settings:new(){["Frontend:RootPath"]=root});
            using var client=factory.CreateApiClient(false);
            Assert.Contains("Kodiak deployment test",await client.GetStringAsync("/"));
            Assert.Contains("Kodiak deployment test",await client.GetStringAsync("/tracking"));
            Assert.Contains("window.kodiak",await client.GetStringAsync("/app.js"));
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/not-an-endpoint")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/missing.js")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/profile/avatar")).StatusCode);
        } finally { Directory.Delete(root,true); }
    }
}
