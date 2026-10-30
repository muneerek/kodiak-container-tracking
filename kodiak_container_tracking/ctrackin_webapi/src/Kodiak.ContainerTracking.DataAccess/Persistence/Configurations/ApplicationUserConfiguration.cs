using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;
public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.ToTable("applicationUser", t => t.HasCheckConstraint("ck_user_type", "\"userType\" IN (0, 1, 2, 3)"));
        b.HasKey(x => x.AppUserId);
        b.Property(x => x.UserName).HasMaxLength(256).IsRequired();
        b.Property(x => x.NormalizedUserName).HasMaxLength(256).IsRequired();
        b.HasIndex(x => x.NormalizedUserName).IsUnique();
        b.Property(x => x.LinkedUniqueCode).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.LinkedUniqueCode);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne(x => x.Reference).WithMany().HasForeignKey(x => new { x.LinkedUniqueId, x.LinkedUniqueCode })
            .HasPrincipalKey(x => new { x.UniqueId, x.UniqueCode }).OnDelete(DeleteBehavior.Restrict);
        AuditConfiguration.Configure(b);
    }
}
