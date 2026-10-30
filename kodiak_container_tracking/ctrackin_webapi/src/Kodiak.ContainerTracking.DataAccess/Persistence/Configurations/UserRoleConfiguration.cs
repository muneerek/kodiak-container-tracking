using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("userRoles"); b.HasKey(x => x.RoleId);
        b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedCode).HasMaxLength(100).IsRequired();
        b.Property(x => x.RoleName).HasMaxLength(250).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => x.NormalizedCode).IsUnique(); b.Property(x => x.Version).IsConcurrencyToken();
        AuditConfiguration.Configure(b);
    }
}
