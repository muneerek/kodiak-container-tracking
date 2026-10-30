using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Kodiak.ContainerTracking.Api.Infrastructure;
using Npgsql;
namespace Kodiak.ContainerTracking.Tests;
public sealed class SetupPostgreSqlTests
{
    [PostgreSqlFact]
    public async Task Creates_dedicated_database_and_bootstraps_idempotently_through_local_cli()
    {
        var admin=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("KODIAK_TEST_POSTGRES")) {Database="postgres",Pooling=false};
        var name="kodiak_setup_test_"+Guid.NewGuid().ToString("N")[..20];
        var request=new SetupRequest("create",admin.Host!,admin.Port,admin.Username!,admin.Password!,name,admin.SslMode.ToString());
        string? role=null;
        try {
            using var created=JsonDocument.Parse(JsonSerializer.Serialize(await SetupTasks.ConnectAsync(request),SetupTasks.JsonOptions));
            role=created.RootElement.GetProperty("userName").GetString();
            var appConnection=created.RootElement.GetProperty("connectionString").GetString()!;
            Assert.EndsWith("_app",role);
            await using(var connection=new NpgsqlConnection(appConnection)) {
                await connection.OpenAsync();
                await using var check=new NpgsqlCommand("SELECT rolsuper OR rolcreatedb OR rolcreaterole FROM pg_roles WHERE rolname=current_user",connection);
                Assert.False((bool)(await check.ExecuteScalarAsync())!);
            }
            var initialize=request with {Action="initialize",ConnectionString=appConnection,AdminPassword="installer-test-password-2026",SeedGeography=false};
            var first=await InitializeAsync(initialize);var second=await InitializeAsync(initialize);
            Assert.Equal(first,second);
            await using(var connection=new NpgsqlConnection(appConnection)) {
                await connection.OpenAsync();
                await using var count=new NpgsqlCommand("SELECT count(*) FROM ctracking.\"applicationUser\"",connection);
                Assert.Equal(1L,(long)(await count.ExecuteScalarAsync())!);
            }
        } finally {
            // Only the unique test database/role generated above can be removed.
            Assert.StartsWith("kodiak_setup_test_",name);
            await using var cleanup=new NpgsqlConnection(admin.ConnectionString);await cleanup.OpenAsync();
            await using(var drop=new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\"",cleanup)) await drop.ExecuteNonQueryAsync();
            if(role==name+"_app") {await using var drop=new NpgsqlCommand($"DROP ROLE IF EXISTS \"{role}\"",cleanup);await drop.ExecuteNonQueryAsync();}
        }
    }
    private static async Task<int> InitializeAsync(SetupRequest request)
    {
        var info=new ProcessStartInfo("dotnet") {UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false)};
        info.ArgumentList.Add(typeof(Program).Assembly.Location);info.ArgumentList.Add("--setup-task");
        info.Environment["ASPNETCORE_ENVIRONMENT"]="Production";
        using var process=Process.Start(info)!;
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request,SetupTasks.JsonOptions));process.StandardInput.Close();
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try {await process.WaitForExitAsync(timeout.Token);}catch {process.Kill(true);throw;}
        var json=await output;await error;
        Assert.True(process.ExitCode==0,"The installer CLI could not initialize its isolated test database: "+json);
        using var result=JsonDocument.Parse(json);Assert.True(result.RootElement.GetProperty("ok").GetBoolean());
        return result.RootElement.GetProperty("adminId").GetInt32();
    }
}
