using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kodiak.ContainerTracking.DataAccess.Persistence;

public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
{
    public DbSet<ContainerRegistration> ContainerRegistrations => Set<ContainerRegistration>();
    public DbSet<ContainerAttachment> ContainerAttachments => Set<ContainerAttachment>();
    public DbSet<UserLoginDetails> LoginDetails => Set<UserLoginDetails>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<OrganizationHierarchy> Organizations => Set<OrganizationHierarchy>();
    public DbSet<CountryMaster> Countries => Set<CountryMaster>();
    public DbSet<TimeZoneMaster> TimeZones => Set<TimeZoneMaster>();
    public DbSet<ShippingPort> ShippingPorts => Set<ShippingPort>();
    public DbSet<StateMaster> States => Set<StateMaster>();
    public DbSet<BusinessCategory> BusinessCategories => Set<BusinessCategory>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Consignee> Consignees => Set<Consignee>();
    public DbSet<Shipper> Shippers => Set<Shipper>();
    public DbSet<PartyReference> PartyReferences => Set<PartyReference>();
    public DbSet<UniqueCodeCounter> UniqueCodeCounters => Set<UniqueCodeCounter>();
    public DbSet<UserRole> Roles => Set<UserRole>();
    public DbSet<UserRoleMapping> UserRoleMappings => Set<UserRoleMapping>();
    public DbSet<AccessAuditEvent> AccessAuditEvents => Set<AccessAuditEvent>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectAuditHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtectAuditHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ProtectAuditHistory()
    {
        if (ChangeTracker.Entries<AccessAuditEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Access audit events are append-only.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ctracking");
        if (Database.IsNpgsql()) modelBuilder.HasSequence<long>("containerRegistrationNumbers", "ctracking");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackingDbContext).Assembly);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            var column = property.Name == nameof(PartyRecord.UniqueId) ? entity.ClrType.Name switch
            {
                nameof(Employee) => "employeeId", nameof(Consignee) => "consigneeId", nameof(Shipper) => "shipperId", _ => "uniqueId"
            } : char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            if (property.Name == nameof(PartyRecord.Name) && (entity.ClrType == typeof(Consignee) || entity.ClrType == typeof(Shipper)))
                column = "companyName";
            if (property.Name == nameof(MasterRecord.Id)) column = entity.ClrType.Name switch
            {
                nameof(Company) => "companyId", nameof(Designation) => "designationId", nameof(OrganizationHierarchy) => "orgId",
                nameof(ShippingPort) => "portId", nameof(CountryMaster) => "countryId", nameof(StateMaster) => "stateId", nameof(BusinessCategory) => "businessCategoryId", _ => column
            };
            if (entity.ClrType == typeof(UserRoleMapping) && property.Name == nameof(UserRoleMapping.MappingRoleId)) column = "mapinRoleId";
            property.SetColumnName(column);
        }
    }
}
