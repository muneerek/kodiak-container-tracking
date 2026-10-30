using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
namespace Kodiak.ContainerTracking.Api.Infrastructure;

// Invoked only as a local command-line task. No setup routes are exposed over HTTP.
public sealed record SetupRequest(string Action, string Host = "localhost", int Port = 5432,
    string UserName = "postgres", string Password = "", string Database = "postgres",
    string SslMode = "Prefer", string AdminName = "superadmin", string AdminPassword = "", bool SeedGeography = true, string? ConnectionString = null);
public static partial class SetupTasks
{
    public static string Connection(SetupRequest input)
    {
        if (!string.IsNullOrWhiteSpace(input.ConnectionString)) return new NpgsqlConnectionStringBuilder(input.ConnectionString) { IncludeErrorDetail=false, Pooling=false, Timeout=10, CommandTimeout=120 }.ConnectionString;
        if (string.IsNullOrWhiteSpace(input.Host) || input.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(input.UserName))
            throw new ArgumentException("Enter a database host, port and username.");
        if (!Enum.TryParse<SslMode>(input.SslMode, true, out var ssl) || !Enum.IsDefined(ssl))
            throw new ArgumentException("Select a valid PostgreSQL TLS mode.");
        return new NpgsqlConnectionStringBuilder { Host=input.Host, Port=input.Port, Username=input.UserName,
            Password=input.Password, Database=input.Database, SslMode=ssl, Timeout=10, CommandTimeout=120,
            IncludeErrorDetail=false, Pooling=false }.ConnectionString;
    }
    public static void ValidateNewDatabase(string database)
    {
        if (!DatabaseName().IsMatch(database) || database is "postgres" or "template0" or "template1")
            throw new ArgumentException("Use a new database name with 1-48 lowercase letters, digits or underscores, beginning with a letter.");
    }
    public static async Task<object> ConnectAsync(SetupRequest input)
    {
        if (input.Action is not ("test" or "list" or "create" or "connection")) throw new ArgumentException("Unknown setup task.");
        if (input.Action=="connection") return new {ok=true,connectionString=Connection(input)};
        var admin=input with { Database="postgres" };
        await using var connection=new NpgsqlConnection(Connection(input.Action=="create"?admin:input));
        await connection.OpenAsync();
        if (input.Action=="test") return new { ok=true, version=connection.PostgreSqlVersion.ToString() };
        if (input.Action=="list") {
            await using var cmd=new NpgsqlCommand("SELECT datname FROM pg_database WHERE NOT datistemplate AND has_database_privilege(datname, 'CONNECT') ORDER BY datname",connection);
            await using var reader=await cmd.ExecuteReaderAsync();var names=new List<string>();
            while(await reader.ReadAsync()) names.Add(reader.GetString(0));return new {ok=true,databases=names};
        }
        ValidateNewDatabase(input.Database);
        var role=input.Database+"_app";
        await using(var exists=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname=@db) OR EXISTS(SELECT 1 FROM pg_roles WHERE rolname=@role)",connection)) {
            exists.Parameters.AddWithValue("db",input.Database);exists.Parameters.AddWithValue("role",role);
            if ((bool)(await exists.ExecuteScalarAsync())!) throw new ArgumentException("This database or its application role already exists. Select another name or connect to the existing database using its application credentials.");
        }
        var password=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(36));
        // Identifiers are strictly validated; generated passwords contain only base64 characters.
        await using(var cmd=new NpgsqlCommand($"CREATE ROLE \"{role}\" LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE",connection)) await cmd.ExecuteNonQueryAsync();
        try {
            await using var cmd=new NpgsqlCommand($"CREATE DATABASE \"{input.Database}\" OWNER \"{role}\" TEMPLATE template0 ENCODING 'UTF8'",connection);
            await cmd.ExecuteNonQueryAsync();
        } catch {
            await using var cleanup=new NpgsqlCommand($"DROP ROLE \"{role}\"",connection);await cleanup.ExecuteNonQueryAsync();throw;
        }
        return new {ok=true,connectionString=Connection(input with {UserName=role,Password=password}),userName=role,password};
    }
    public static string SafeError(Exception exception) => exception switch {
        ArgumentException e => e.Message,
        Kodiak.ContainerTracking.Business.Exceptions.BusinessException e => e.Message,
        PostgresException e => $"PostgreSQL rejected the operation ({e.SqlState}). Check credentials, database ownership and CREATE DATABASE/CREATE ROLE permissions.",
        NpgsqlException => "Cannot connect to PostgreSQL. Check the host, port, TLS setting, firewall and server availability.",
        _ => "Database setup failed. Check database availability and permissions. Existing databases were not deleted."
    };
    public static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);
    [GeneratedRegex("^[a-z][a-z0-9_]{0,47}$",RegexOptions.CultureInvariant)]
    private static partial Regex DatabaseName();
}
