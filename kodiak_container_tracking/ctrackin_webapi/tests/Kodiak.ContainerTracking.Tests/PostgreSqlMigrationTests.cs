using Kodiak.ContainerTracking.Api.Infrastructure;
using Kodiak.ContainerTracking.Business.Configuration;
using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Kodiak.ContainerTracking.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;
namespace Kodiak.ContainerTracking.Tests;
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KODIAK_TEST_POSTGRES"))) Skip = "Set KODIAK_TEST_POSTGRES for an isolated PostgreSQL server."; }
}
public sealed class PostgreSqlMigrationTests
{
    [PostgreSqlFact]
    public Task Tracking_migration_and_event_attachment_relationship_work_on_postgres() => InDatabaseAsync(async db =>
    {
        await db.Database.MigrateAsync();
        var masters = new MasterRepository(db); var parties = new PartyRepository(db); var users = new UserManagementRepository(db);
        var work = new TrackingUnitOfWork(db); var actor = new Actor();
        var masterService = new MasterService(masters, work, actor);
        var country = await masterService.CreateAsync(MasterKind.Countries, new Kodiak.ContainerTracking.Business.Contracts.MasterRequest("ZZ", "Tracking country"), default);
        var origin = await masterService.CreateAsync(MasterKind.ShippingPorts, new Kodiak.ContainerTracking.Business.Contracts.MasterRequest(null,"Origin",CountryId:country.Id),default);
        var destination = await masterService.CreateAsync(MasterKind.ShippingPorts, new Kodiak.ContainerTracking.Business.Contracts.MasterRequest(null,"Destination",CountryId:country.Id),default);
        var partyService = new PartyService(parties, masters, users, work, actor, Options.Create(new UniqueCodeOptions()), new TechnicalAccountAccess(users,actor), new ShipperAccess(users,actor));
        var consignee = await partyService.CreateCompanyAsync(PartyType.Consignee,TestData.Consignee(),default);
        var shipper = await partyService.CreateShipperAsync(TestData.Shipper(consignee.UniqueId),default);
        var service = new ContainerService(new ContainerRepository(db),parties,masters,users,work,actor,Options.Create(new ContainerStorageOptions()));
        var item = await service.CreateAsync(new Kodiak.ContainerTracking.Business.Contracts.ContainerRequest("40HC","Cargo",100,10,origin.Id,destination.Id,new(2026,1,1),new(2026,11,10),ShipperId:shipper.UniqueId),default);
        var updated = await service.UpdateTrackingAsync(item.Id,long.Parse(item.Version),new Kodiak.ContainerTracking.Business.Contracts.ContainerTrackingRequest(ContainerTrackingStatus.InTransit,DateTimeOffset.UtcNow.AddMinutes(-1),"At sea",new(2026,11,10)),
            new[] { new PreparedContainerFile("containers/test.pdf","evidence.pdf","application/pdf",100) },default);
        var entry = Assert.Single(updated.TrackingEvents); Assert.Equal(entry.Id,Assert.Single(updated.Files).TrackingEventId);
        db.ChangeTracker.Clear(); var loaded = await service.GetAsync(item.Id,default);
        Assert.Equal(ContainerTrackingStatus.InTransit,loaded.TrackingStatus); Assert.Single(Assert.Single(loaded.TrackingEvents).Files);
        var dashboard = await new OverviewRepository(db).ReadAsync(DateOnly.FromDateTime(DateTime.UtcNow),default);
        Assert.Equal(1,dashboard.Counts.Total);Assert.Equal(1,dashboard.Counts.InTransit);Assert.Equal(1,dashboard.Counts.Routes);
        Assert.Equal(1,Assert.Single(dashboard.Stages).Count);Assert.Single(dashboard.Recent);Assert.Equal("tracking",dashboard.Activity[0].Kind);
        var supplier = await new OverviewRepository(db).ReadAsync(DateOnly.FromDateTime(DateTime.UtcNow),default,new ContainerScope(false,shipper.UniqueId));
        Assert.Equal(1,supplier.Counts.Total);Assert.Equal(1,supplier.Counts.Shippers);Assert.Equal(1,supplier.Counts.Consignees);
        var distributor = await new OverviewRepository(db).ReadAsync(DateOnly.FromDateTime(DateTime.UtcNow),default,new ContainerScope(false,null,consignee.UniqueId));
        Assert.Equal(1,distributor.Counts.Total);Assert.Equal(1,distributor.PartnerCount);Assert.Equal(1,Assert.Single(distributor.Partners).Containers);
        var unrelated = await new OverviewRepository(db).ReadAsync(DateOnly.FromDateTime(DateTime.UtcNow),default,new ContainerScope(false,shipper.UniqueId+10000));
        Assert.Equal(0,unrelated.Counts.Total);Assert.Empty(unrelated.Activity);Assert.Empty(unrelated.Recent);Assert.Empty(unrelated.Routes);
    });
    [PostgreSqlFact]
    public Task Concurrent_shipping_ports_receive_unique_generated_codes() => InDatabaseAsync(async db =>
    {
        await db.Database.MigrateAsync();
        var service = new MasterService(new MasterRepository(db), new TrackingUnitOfWork(db), new Actor());
        var country = await service.CreateAsync(MasterKind.Countries, new Kodiak.ContainerTracking.Business.Contracts.MasterRequest("ZZ", "Test country"), default);
        var connection = db.Database.GetConnectionString()!;
        var tasks = Enumerable.Range(1, 6).Select(async i =>
        {
            await using var context = new TrackingDbContext(new DbContextOptionsBuilder<TrackingDbContext>().UseNpgsql(connection).Options);
            var ports = new MasterService(new MasterRepository(context), new TrackingUnitOfWork(context), new Actor());
            return await ports.CreateAsync(MasterKind.ShippingPorts,
                new Kodiak.ContainerTracking.Business.Contracts.MasterRequest(null, $"Port {i}", CountryId: country.Id), default);
        });
        var results = await Task.WhenAll(tasks);
        Assert.Equal(6, results.Select(x => x.Code).Distinct().Count());
        Assert.All(results, x => Assert.Matches("^PORT-[0-9]{6}$", x.Code));
    });
    [PostgreSqlFact]
    public Task Timezone_directory_contains_every_postgres_zone_and_fractional_offsets() => InDatabaseAsync(async db =>
    {
        await db.Database.MigrateAsync();
        var expected = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::integer AS \"Value\" FROM pg_timezone_names").SingleAsync();
        Assert.Equal(expected, await db.TimeZones.CountAsync());
        Assert.Equal(19800, (await db.TimeZones.SingleAsync(x => x.Name == "Asia/Kolkata")).UtcOffsetSeconds);
        Assert.Equal(20700, (await db.TimeZones.SingleAsync(x => x.Name == "Asia/Kathmandu")).UtcOffsetSeconds);
        Assert.Equal(0, (await db.TimeZones.SingleAsync(x => x.Name == "UTC")).UtcOffsetSeconds);
    });
    [PostgreSqlFact]
    public Task Deployment_script_can_be_applied_twice() => InDatabaseAsync(async db =>
    {
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        await db.Database.ExecuteSqlRawAsync(script);
        await db.Database.ExecuteSqlRawAsync(script);
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.True(await Bootstrap(db).EnsureSuperAdminAsync(default) > 0);
    });
    [PostgreSqlFact]
    public Task Fresh_database_migrates_and_bootstraps_once() => InDatabaseAsync(async db =>
    {
        await db.Database.MigrateAsync(); var bootstrap = Bootstrap(db); var id = await bootstrap.EnsureSuperAdminAsync(default);
        Assert.Equal(id, await bootstrap.EnsureSuperAdminAsync(default)); Assert.Equal(1, await db.Employees.CountAsync());
        Assert.Equal(UserType.Admin, (await db.Users.SingleAsync()).UserType); Assert.Equal(1, await db.UserRoleMappings.CountAsync());
    });
    [PostgreSqlFact]
    public Task Upgrade_preserves_ids_mappings_profiles_and_does_not_promote_internal_users() => InDatabaseAsync(async db =>
    {
        var previous = db.Database.GetMigrations().Single(x => x.EndsWith("_EnforceRequiredPartyDetails", StringComparison.Ordinal));
        await db.GetService<IMigrator>().MigrateAsync(previous);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO ctracking."partyReferences" ("uniqueId", "userType", "uniqueCode") VALUES (101, 1, 'EMP-000001');
            INSERT INTO ctracking."Employees" ("employeeId", "userType", "uniqueCode", "name", "firstName", "lastName", "dob", "gender", "emailId", "mobileNumber", "address", "designation", "version", "createOn", "updatedOn")
            VALUES (101, 1, 'EMP-000001', 'Legacy Employee', 'Legacy', 'Employee', '1990-01-01', 'Female', 'legacy@example.com', '+919876543210', 'Legacy Road', 'Coordinator', 1, now(), now());
            INSERT INTO ctracking."applicationUsers" ("userId", "userType", "uniqueId", "uniqueCode", "userName", "normalizedUserName", "userPassword", "version", "createOn", "updatedOn")
            VALUES (101, 1, 101, 'EMP-000001', 'legacy', 'LEGACY', 'legacy-password-hash', 4, now(), now());
            UPDATE ctracking."applicationUsers" SET "createdBy" = 101, "updatedBy" = 101;
            INSERT INTO ctracking."userRoles" ("roleId", "code", "normalizedCode", "roleName", "userType", "isActive", "version", "createOn", "updatedOn", "createdBy", "updatedBy")
            VALUES (1, 'OPS', 'OPS', 'Operations', 1, true, 1, now(), now(), 101, 101);
            INSERT INTO ctracking."userRoleMapping" ("mappingId", "fkUserId", "fkRoleId", "userType", "createOn", "updatedOn", "createdBy", "updatedBy") VALUES (17, 101, 1, 1, now(), now(), 101, 101);
            UPDATE ctracking."uniqueCodeCounters" SET "lastNumber" = 1 WHERE "userType" = 1;
            SELECT setval(pg_get_serial_sequence('ctracking."partyReferences"', 'uniqueId'), 101, true);
            SELECT setval(pg_get_serial_sequence('ctracking."applicationUsers"', 'userId'), 101, true);
            SELECT setval(pg_get_serial_sequence('ctracking."userRoles"', 'roleId'), 1, true);
            """);
        await db.Database.MigrateAsync();
        var user = await new UserManagementRepository(db).GetUserAsync(101, default); Assert.NotNull(user);
        Assert.Equal(101, user.AppUserId); Assert.Equal(101, user.LinkedUniqueId); Assert.Equal("EMP-000001", user.LinkedUniqueCode);
        Assert.Equal(UserType.Normal, user.UserType); Assert.Equal("legacy-password-hash", user.UserPassword); Assert.Equal("101", user.CreatedBy);
        Assert.Equal(17, Assert.Single(user.RoleMappings).MappingRoleId); Assert.Equal(new DateOnly(1990, 1, 1), user.Reference.Employee!.Dob);
        var archived = await db.Database.SqlQueryRaw<string>("SELECT data->>'gender' AS \"Value\" FROM ctracking.\"legacyStructureArchive\" WHERE \"sourceTable\" = 'Employees' AND \"sourceId\" = 101").SingleAsync();
        Assert.Equal("Female", archived); var admin = await Bootstrap(db).EnsureSuperAdminAsync(default); Assert.True(admin > 101);
    });
    [PostgreSqlFact]
    public Task Concurrent_master_creation_keeps_codes_unique() => InDatabaseAsync(async db =>
    {
        await db.Database.MigrateAsync(); var connection = db.Database.GetConnectionString()!;
        var tasks = Enumerable.Range(1, 6).Select(async i =>
        {
            await using var context = new TrackingDbContext(new DbContextOptionsBuilder<TrackingDbContext>().UseNpgsql(connection).Options);
            var service = new PartyService(new PartyRepository(context), new MasterRepository(context), new UserManagementRepository(context), new TrackingUnitOfWork(context), new Actor(), Options.Create(new UniqueCodeOptions()), new TechnicalAccountAccess(new UserManagementRepository(context), new Actor()), new ShipperAccess(new UserManagementRepository(context), new Actor()));
            return await service.CreateEmployeeAsync(TestData.Employee($"Employee {i}"), default);
        });
        Assert.Equal(6, (await Task.WhenAll(tasks)).Select(x => x.UniqueCode).Distinct().Count());
    });
    private static BootstrapService Bootstrap(TrackingDbContext db) => new(new MasterRepository(db), new PartyRepository(db), new UserManagementRepository(db), new TrackingUnitOfWork(db),
        new PasswordService(), Options.Create(new BootstrapOptions { InitialPassword = TestData.Password }), Options.Create(new UniqueCodeOptions()));
    private sealed class Actor : ICurrentActor { public long? UserId => null; public string Subject => "postgres-test"; public bool IsSuperAdmin => true; }
    private static async Task InDatabaseAsync(Func<TrackingDbContext, Task> test)
    {
        var configuration = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("KODIAK_TEST_POSTGRES"))
        { Database = "postgres", Pooling = false };
        await using var admin = new NpgsqlConnection(configuration.ConnectionString);
        await admin.OpenAsync();
        var database = "kodiak_test_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            configuration.Database = database;
            await using var db = new TrackingDbContext(new DbContextOptionsBuilder<TrackingDbContext>().UseNpgsql(configuration.ConnectionString).Options);
            await test(db);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\"", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
