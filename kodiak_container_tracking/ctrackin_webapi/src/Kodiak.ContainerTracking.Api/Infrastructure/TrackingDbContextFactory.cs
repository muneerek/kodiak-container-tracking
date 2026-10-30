using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Kodiak.ContainerTracking.Api.Infrastructure;

public sealed class TrackingDbContextFactory : IDesignTimeDbContextFactory<TrackingDbContext>
{
    public TrackingDbContext CreateDbContext(string[] args)
    {
        var current = Directory.GetCurrentDirectory();
        var project = Path.Combine(current, "src", "Kodiak.ContainerTracking.Api");
        var root = File.Exists(Path.Combine(current, "appsettings.json")) ? current : Directory.Exists(project) ? project : AppContext.BaseDirectory;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = root,
            ApplicationName = typeof(Program).Assembly.GetName().Name
        });
        builder.Configuration.AddUserSecrets<Program>(optional: true).AddEnvironmentVariables();
        return new TrackingDbContext(new DbContextOptionsBuilder<TrackingDbContext>().UseNpgsql(
            builder.Configuration.GetConnectionString("TrackingDatabase") ?? throw new InvalidOperationException("Configure TrackingDatabase.")).Options);
    }
}
