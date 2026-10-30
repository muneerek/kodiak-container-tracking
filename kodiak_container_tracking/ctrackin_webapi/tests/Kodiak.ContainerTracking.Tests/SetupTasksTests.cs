using Kodiak.ContainerTracking.Api.Infrastructure;
using Npgsql;
namespace Kodiak.ContainerTracking.Tests;
public sealed class SetupTasksTests
{
    [Theory]
    [InlineData("kodiak")]
    [InlineData("kodiak_2026")]
    public void Accepts_safe_new_database_names(string name) => SetupTasks.ValidateNewDatabase(name);
    [Theory]
    [InlineData("postgres")]
    [InlineData("template0")]
    [InlineData("x\"; DROP DATABASE postgres; --")]
    [InlineData("")]
    [InlineData("has space")]
    public void Rejects_reserved_or_unsafe_names(string name) => Assert.Throws<ArgumentException>(()=>SetupTasks.ValidateNewDatabase(name));
    [Fact]
    public void Connection_builder_preserves_special_characters_without_parameter_injection()
    {
        var input=new SetupRequest("test",Password:"a;Password=other;\"quoted",UserName:"application",Database:"kodiak");
        var connection=new NpgsqlConnectionStringBuilder(SetupTasks.Connection(input));
        Assert.Equal(input.Password,connection.Password);Assert.Equal("application",connection.Username);
        Assert.Equal("kodiak",connection.Database);Assert.False(connection.IncludeErrorDetail);
    }
    [Fact]
    public async Task Connection_task_does_not_require_a_running_server()
    {
        var result=await SetupTasks.ConnectAsync(new SetupRequest("connection",Host:"nonexistent.invalid",Password:"private"));
        var json=System.Text.Json.JsonSerializer.Serialize(result,SetupTasks.JsonOptions);
        Assert.Contains("connectionString",json);Assert.Contains("nonexistent.invalid",json);
    }
    [Fact]
    public void Database_errors_do_not_echo_passwords()
    {
        Assert.DoesNotContain("secret-password",SetupTasks.SafeError(new NpgsqlException("failed with secret-password")));
        Assert.DoesNotContain("secret-password",SetupTasks.SafeError(new InvalidOperationException("secret-password")));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Rejects_invalid_database_ports(int port) => Assert.Throws<ArgumentException>(()=>SetupTasks.Connection(new SetupRequest("test",Port:port)));
}
