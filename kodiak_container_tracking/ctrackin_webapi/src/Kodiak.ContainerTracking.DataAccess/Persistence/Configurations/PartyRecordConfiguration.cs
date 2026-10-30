using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;
internal static class PartyRecordConfiguration
{
    internal static void Configure<T>(EntityTypeBuilder<T> b, string table, int type) where T : PartyRecord
    {
        b.HasBaseType((Type?)null); b.ToTable(table, t => t.HasCheckConstraint($"ck_{table}_type", $"\"partyType\" = {type}"));
        b.HasKey(x => x.UniqueId); b.Property(x => x.UniqueId).ValueGeneratedNever();
        b.Property(x => x.UniqueCode).HasMaxLength(50).IsRequired();
        if (typeof(T) != typeof(Employee))
        {
            b.Property(x => x.Name).HasMaxLength(300);
            b.Property(x => x.Address).HasMaxLength(2000);
        }
        b.Property(x => x.EmailId).HasMaxLength(150); b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.UniqueCode).IsUnique(); AuditConfiguration.Configure(b);
    }
}
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        PartyRecordConfiguration.Configure(b, "employeeDetails", 1);
        b.Ignore(x => x.Name); b.Ignore(x => x.Address);
        b.Property(x => x.FirstName).HasMaxLength(300).IsRequired(); b.Property(x => x.LastName).HasMaxLength(300);
        b.ToTable("employeeDetails", t => t.HasCheckConstraint("ck_employee_status", "\"userStatus\" IN (1, 2)"));
        b.HasOne(x => x.Designation).WithMany().HasForeignKey(x => x.DesignationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Reference).WithOne(x => x.Employee).HasForeignKey<Employee>(x => new { x.UniqueId, x.PartyType, x.UniqueCode })
            .HasPrincipalKey<PartyReference>(x => new { x.UniqueId, x.PartyType, x.UniqueCode }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ConsigneeConfiguration : IEntityTypeConfiguration<Consignee>
{
    public void Configure(EntityTypeBuilder<Consignee> b)
    {
        PartyRecordConfiguration.Configure(b, "consigneeDetails", 2);
        b.Property(x => x.LastName).HasMaxLength(300); b.Property(x => x.PhoneNumber).HasMaxLength(30);
        b.ToTable("consigneeDetails", t => t.HasCheckConstraint("ck_consignee_status", "\"status\" IN (1, 2)"));
        b.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Reference).WithOne(x => x.Consignee).HasForeignKey<Consignee>(x => new { x.UniqueId, x.PartyType, x.UniqueCode })
            .HasPrincipalKey<PartyReference>(x => new { x.UniqueId, x.PartyType, x.UniqueCode }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ShipperConfiguration : IEntityTypeConfiguration<Shipper>
{
    public void Configure(EntityTypeBuilder<Shipper> b)
    {
        PartyRecordConfiguration.Configure(b, "shipperDetails", 3);
        b.Property(x => x.LastName).HasMaxLength(300); b.Property(x => x.PhoneNumber).HasMaxLength(30);
        b.ToTable("shipperDetails", t => t.HasCheckConstraint("ck_shipper_status", "\"status\" IN (1, 2)"));
        b.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BusinessCategory).WithMany().HasForeignKey(x => x.BusinessCategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Consignee).WithMany().HasForeignKey(x => x.ConsigneeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Reference).WithOne(x => x.Shipper).HasForeignKey<Shipper>(x => new { x.UniqueId, x.PartyType, x.UniqueCode })
            .HasPrincipalKey<PartyReference>(x => new { x.UniqueId, x.PartyType, x.UniqueCode }).OnDelete(DeleteBehavior.Restrict);
    }
}
