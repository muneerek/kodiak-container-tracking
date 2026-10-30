using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;
public sealed class ContainerTrackingConfiguration : IEntityTypeConfiguration<ContainerTrackingEvent>
{
    public void Configure(EntityTypeBuilder<ContainerTrackingEvent> b)
    {
        b.ToTable("containerTrackingEvent"); b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.Id, x.ContainerRegistrationId });
        b.Property(x => x.Location).HasMaxLength(200).IsRequired();
        b.Property(x => x.VesselName).HasMaxLength(150); b.Property(x => x.Note).HasMaxLength(2000);
        b.HasIndex(x => new { x.ContainerRegistrationId, x.EventAt });
        b.HasOne(x => x.Registration).WithMany(x => x.TrackingEvents).HasForeignKey(x => x.ContainerRegistrationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Files).WithOne(x => x.TrackingEvent).HasForeignKey(x => new { x.TrackingEventId, x.ContainerRegistrationId })
            .HasPrincipalKey(x => new { x.Id, x.ContainerRegistrationId }).OnDelete(DeleteBehavior.Restrict);
        AuditConfiguration.Configure(b);
    }
}
