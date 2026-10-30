using System.Security.Claims;
using Kodiak.ContainerTracking.Business.Seeding;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Kodiak.ContainerTracking.Api.Infrastructure;
using Kodiak.ContainerTracking.Business.Configuration;
using Kodiak.ContainerTracking.Business.Services;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Kodiak.ContainerTracking.DataAccess.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authorization;

SetupRequest? setupRequest = null;
if (args.Contains("--setup-task", StringComparer.Ordinal))
{
    Console.InputEncoding=new UTF8Encoding(false);
    try {
        setupRequest = System.Text.Json.JsonSerializer.Deserialize<SetupRequest>(await Console.In.ReadToEndAsync(), SetupTasks.JsonOptions)
            ?? throw new ArgumentException("Setup input is required.");
        if (setupRequest.Action != "initialize") {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await SetupTasks.ConnectAsync(setupRequest), SetupTasks.JsonOptions));
            return;
        }
    } catch (Exception e) {
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {ok=false,error=SetupTasks.SafeError(e)}, SetupTasks.JsonOptions));
        Environment.ExitCode=1;return;
    }
}
var builder = WebApplication.CreateBuilder(args);
if (setupRequest != null) {
    builder.Logging.ClearProviders();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
        ["ConnectionStrings:TrackingDatabase"]=SetupTasks.Connection(setupRequest),
        ["Bootstrap:Enabled"]="false", ["Bootstrap:UserName"]=setupRequest.AdminName,
        ["Bootstrap:InitialPassword"]=setupRequest.AdminPassword,
        ["Jwt:SigningKey"]=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48))
    });
}
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
{ Status = 400, Title = "validation_failed", Extensions = { ["code"] = "validation_failed", ["traceId"] = context.HttpContext.TraceIdentifier } }));
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, context, cancellationToken) =>
{
    document.Info.Title = "Kodiak Container Tracking API";
    document.Components ??= new();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Sign in using /api/auth/login, then paste the accessToken here."
    };
    foreach (var description in context.DescriptionGroups.SelectMany(group => group.Items))
    {
        var metadata = description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any()) continue;
        var path = "/" + description.RelativePath?.Split('?')[0];
        if (document.Paths.TryGetValue(path, out var item) && item.Operations != null)
            foreach (var operation in item.Operations.Where(pair => pair.Key.Method.Equals(description.HttpMethod, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value))
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] });
            }
    }
    return Task.CompletedTask;
}));
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context => context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<ApiExceptionHandler>(); builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, CurrentActor>();
builder.Services.AddScoped<TechnicalAccountAccess>();
builder.Services.AddScoped<ShipperAccess>();
builder.Services.AddScoped<ICompanyProfileService, CompanyProfileService>();
builder.Services.AddScoped<IOverviewRepository, OverviewRepository>(); builder.Services.AddScoped<IOverviewService, OverviewService>();
builder.Services.AddScoped<IContainerRepository, ContainerRepository>(); builder.Services.AddScoped<IContainerService, ContainerService>();
builder.Services.AddScoped<IContainerFileStorage, ContainerFileStorage>();
builder.Services.AddOptions<ContainerStorageOptions>().BindConfiguration("ContainerStorage")
    .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath) && o.MaxFileBytes is >= 65536 and <= 10485760 && o.MaxFilesPerContainer is >= 1 and <= 100, "Configure valid container storage limits.").ValidateOnStart();
builder.Services.AddScoped<IGeographySeedRepository, GeographySeedRepository>();
builder.Services.AddScoped<CountrySeeder>(); builder.Services.AddScoped<StateProvinceSeeder>();
builder.Services.AddScoped<IGeographySeeder, GeographySeeder>();
builder.Services.AddScoped<IRegistrationRepository, RegistrationRepository>();
builder.Services.AddScoped<IShipperRegistrationService, ShipperRegistrationService>();
builder.Services.AddScoped<IEmployeePhotoStorage, EmployeePhotoStorage>();
builder.Services.AddOptions<PhotoStorageOptions>().BindConfiguration("PhotoStorage")
    .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath) && o.MaxFileBytes is >= 65536 and <= 5242880,
        "Configure PhotoStorage:RootPath and MaxFileBytes between 65536 and 5242880.").ValidateOnStart();
builder.Services.AddDbContext<TrackingDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("TrackingDatabase") ?? throw new InvalidOperationException("Configure TrackingDatabase.")));
builder.Services.AddScoped<IUserManagementRepository, UserManagementRepository>(); builder.Services.AddScoped<IPartyRepository, PartyRepository>();
builder.Services.AddScoped<IMasterRepository, MasterRepository>(); builder.Services.AddScoped<IMasterService, MasterService>();
builder.Services.AddScoped<IAuthenticationRepository, AuthenticationRepository>(); builder.Services.AddScoped<IUnitOfWork, TrackingUnitOfWork>();
builder.Services.AddScoped<IPartyService, PartyService>(); builder.Services.AddScoped<UserRoleAssignmentService>();
builder.Services.AddScoped<IUserService, UserService>(); builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>(); builder.Services.AddScoped<IBootstrapService, BootstrapService>();
builder.Services.AddScoped<IUserSessionsService, UserSessionsService>();
builder.Services.AddSingleton<IPasswordService, PasswordService>(); builder.Services.AddSingleton<IAccessTokenIssuer, JwtTokenIssuer>();
builder.Services.AddOptions<UniqueCodeOptions>().BindConfiguration("UniqueCodes").Validate(o => o.IsValid(), "Configure distinct uppercase code prefixes and padding 1?18.").ValidateOnStart();
builder.Services.AddOptions<BootstrapOptions>().BindConfiguration("Bootstrap");
builder.Services.AddOptions<JwtOptions>().BindConfiguration("Jwt").Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32 && !string.IsNullOrWhiteSpace(o.Issuer)
    && !string.IsNullOrWhiteSpace(o.Audience) && o.AccessTokenMinutes is >= 1 and <= 60 && o.RefreshTokenDays is >= 1 and <= 30,
    "Configure Jwt:SigningKey with at least 32 bytes, issuer, audience and valid token lifetimes.").ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new();
    o.MapInboundClaims = false; o.RequireHttpsMetadata = true;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(10), ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = "name", RoleClaimType = "role"
    };
    o.Events = new JwtBearerEvents { OnTokenValidated = async context =>
    {
        if (!int.TryParse(context.Principal?.FindFirstValue("sub"), out var id) || !long.TryParse(context.Principal?.FindFirstValue("ver"), out var version))
        { context.Fail("Invalid account claims."); return; }
        var user = await context.HttpContext.RequestServices.GetRequiredService<IAuthenticationService>().ValidateAsync(id, version, context.HttpContext.RequestAborted);
        if (user == null) { context.Fail("Account or token is no longer available."); return; }
        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(JwtTokenIssuer.Claims(user), "Bearer", "name", "role"));
    } };
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("ContainersAccess", p => p.RequireAuthenticatedUser().RequireAssertion(context => context.User.HasClaim("user_type", "2") || context.User.HasClaim("user_type", "3") || (context.User.HasClaim("user_type", "1") && context.User.HasClaim("permission", "users.manage"))));
    o.AddPolicy("ShippersManage", p => p.RequireAuthenticatedUser().RequireAssertion(context =>
        context.User.HasClaim("user_type", "3") || (context.User.HasClaim("user_type", "1") && context.User.HasClaim("permission", "users.manage"))));
    o.AddPolicy("UsersManage", p => p.RequireAuthenticatedUser().RequireClaim("user_type", "1").RequireClaim("permission", "users.manage"));
    o.AddPolicy("AccessManage", p => p.RequireAuthenticatedUser().RequireClaim("user_type", "1").RequireClaim("permission", "access.manage"));
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("registration", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    o.AddPolicy("registration-options", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});
var app = builder.Build();
var frontendRoot=app.Configuration["Frontend:RootPath"];
if (!string.IsNullOrWhiteSpace(frontendRoot)) {
    app.Environment.WebRootPath=Path.GetFullPath(frontendRoot);
    var frontendFiles=new Microsoft.Extensions.FileProviders.PhysicalFileProvider(app.Environment.WebRootPath);
    app.Environment.WebRootFileProvider=frontendFiles;
    app.Lifetime.ApplicationStopped.Register(frontendFiles.Dispose);
}
if (setupRequest != null) {
    try {
        await using var scope=app.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        await db.Database.MigrateAsync();
        if(setupRequest.SeedGeography) await scope.ServiceProvider.GetRequiredService<IGeographySeeder>().SeedAsync(true,CancellationToken.None);
        var id=await scope.ServiceProvider.GetRequiredService<IBootstrapService>().EnsureSuperAdminAsync(CancellationToken.None);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {ok=true,adminId=id},SetupTasks.JsonOptions));
    } catch(Exception e) {
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {ok=false,error=SetupTasks.SafeError(e)},SetupTasks.JsonOptions));
        Environment.ExitCode=1;
    }
    return;
}
if (args.Any(x => x is "--seed-geography" or "--seed-countries" or "--seed-states"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var includeStates = args.Any(x => x is "--seed-geography" or "--seed-states");
    var result = await scope.ServiceProvider.GetRequiredService<IGeographySeeder>().SeedAsync(includeStates, CancellationToken.None);
    app.Logger.LogInformation("Geography seeding complete. Countries added: {Countries}; states/provinces added: {States}", result.CountriesAdded, result.StatesAdded);
    return;
}
if (args.Contains("--bootstrap-super-admin", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var id = await scope.ServiceProvider.GetRequiredService<IBootstrapService>().EnsureSuperAdminAsync(CancellationToken.None);
    app.Logger.LogInformation("Super-admin setup complete. AppUserId: {AppUserId}", id);
    return;
}
if (builder.Configuration.GetValue<bool>("Bootstrap:Enabled"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IBootstrapService>().EnsureSuperAdminAsync(CancellationToken.None);
}
app.UseExceptionHandler(); app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Kodiak Container Tracking API v1"));
}
if (Directory.Exists(app.Environment.WebRootPath)) { app.UseDefaultFiles(); app.UseStaticFiles(); }
app.UseHttpsRedirection(); app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization(); app.MapControllers();
if (Directory.Exists(app.Environment.WebRootPath)) app.MapFallback(async context => {
    if (context.Request.Path.StartsWithSegments("/api") || Path.HasExtension(context.Request.Path.Value)) { context.Response.StatusCode=404; return; }
    context.Response.ContentType="text/html; charset=utf-8";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath,"index.html"));
});
app.Run();
public partial class Program;
