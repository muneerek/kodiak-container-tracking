using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;
public sealed class UserRoleMappingConfiguration : IEntityTypeConfiguration<UserRoleMapping>
{
    public void Configure(EntityTypeBuilder<UserRoleMapping> b)
    {
        b.ToTable("userRolesmapin"); b.HasKey(x => x.MappingRoleId);
        b.HasIndex(x => new { x.FkAppUserId, x.FkRoleId }).IsUnique();
        b.HasOne(x => x.User).WithMany(x => x.RoleMappings).HasForeignKey(x => x.FkAppUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Role).WithMany(x => x.UserMappings).HasForeignKey(x => x.FkRoleId).OnDelete(DeleteBehavior.Restrict);
        AuditConfiguration.Configure(b);
    }
}
