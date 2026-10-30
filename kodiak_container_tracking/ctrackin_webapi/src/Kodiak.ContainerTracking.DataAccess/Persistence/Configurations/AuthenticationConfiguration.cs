using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;
public sealed class UserLoginDetailsConfiguration : IEntityTypeConfiguration<UserLoginDetails>
{
    public void Configure(EntityTypeBuilder<UserLoginDetails> b)
    {
        b.ToTable("userLoginDetails", t => t.HasCheckConstraint("ck_login_times", "\"logoutTime\" IS NULL OR \"logoutTime\" >= \"loginTime\""));
        b.HasKey(x => x.LoginId); b.HasIndex(x => new { x.FkAppUserId, x.LoginTime });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.FkAppUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> b)
    {
        b.ToTable("userTokens"); b.HasKey(x => x.TokenId);
        b.Property(x => x.Purpose).HasMaxLength(30).IsRequired(); b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique(); b.Property(x => x.UsedOn).IsConcurrencyToken();
        b.Property(x => x.RevokedOn).IsConcurrencyToken();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.FkAppUserId).OnDelete(DeleteBehavior.Restrict);
        AuditConfiguration.Configure(b);
    }
}
