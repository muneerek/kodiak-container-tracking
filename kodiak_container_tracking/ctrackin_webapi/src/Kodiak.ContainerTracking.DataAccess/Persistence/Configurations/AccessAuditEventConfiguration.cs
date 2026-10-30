using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;

public sealed class AccessAuditEventConfiguration : IEntityTypeConfiguration<AccessAuditEvent>
{
    public void Configure(EntityTypeBuilder<AccessAuditEvent> builder)
    {
        builder.ToTable("accessAuditEvents");
        builder.HasKey(x => x.EventId);
        builder.Property(x => x.ActorSubject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Changes).IsRequired();
    }
}
