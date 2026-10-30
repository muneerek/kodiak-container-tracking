using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;
internal static class MasterConfiguration
{
    internal static void Apply<T>(EntityTypeBuilder<T> b, string table) where T : MasterRecord
    {
        b.HasBaseType((Type?)null); b.ToTable(table); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired(); b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.HasIndex(x => x.Code).IsUnique(); b.Property(x => x.Version).IsConcurrencyToken(); AuditConfiguration.Configure(b);
    }
}
public sealed class CountryConfiguration : IEntityTypeConfiguration<CountryMaster>
{ public void Configure(EntityTypeBuilder<CountryMaster> b) => MasterConfiguration.Apply(b, "conuntryMaster"); }
public sealed class StateConfiguration : IEntityTypeConfiguration<StateMaster>
{
    public void Configure(EntityTypeBuilder<StateMaster> b)
    {
        MasterConfiguration.Apply(b, "StateMaster");
        b.HasAlternateKey(x => new { x.Id, x.CountryId });
        b.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{ public void Configure(EntityTypeBuilder<Designation> b) => MasterConfiguration.Apply(b, "designation"); }
public sealed class BusinessCategoryConfiguration : IEntityTypeConfiguration<BusinessCategory>
{ public void Configure(EntityTypeBuilder<BusinessCategory> b) => MasterConfiguration.Apply(b, "businessCategory"); }
public sealed class OrganizationConfiguration : IEntityTypeConfiguration<OrganizationHierarchy>
{
    public void Configure(EntityTypeBuilder<OrganizationHierarchy> b)
    {
        MasterConfiguration.Apply(b, "organizationHirarchy");
        b.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentOrgId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        MasterConfiguration.Apply(b, "company"); b.Property(x => x.EmailId).HasMaxLength(150);
        b.Property(x => x.PhoneNumber).HasMaxLength(30); b.Property(x => x.Address).HasMaxLength(2000);
        b.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.State).WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShippingPortConfiguration : IEntityTypeConfiguration<ShippingPort>
{
    public void Configure(EntityTypeBuilder<ShippingPort> b)
    {
        MasterConfiguration.Apply(b, "shippingPort");
        b.Property(x => x.City).HasMaxLength(150);
        b.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.State).WithMany().HasForeignKey(x => new { x.StateId, x.CountryId })
            .HasPrincipalKey(x => new { x.Id, x.CountryId }).OnDelete(DeleteBehavior.Restrict);
    }
}
