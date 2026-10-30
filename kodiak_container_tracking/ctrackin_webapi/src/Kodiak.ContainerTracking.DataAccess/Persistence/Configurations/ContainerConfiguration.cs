using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;
public sealed class ContainerConfiguration : IEntityTypeConfiguration<ContainerRegistration>
{
    public void Configure(EntityTypeBuilder<ContainerRegistration> b)
    {
        b.ToTable("containerRegistration"); b.HasKey(x => x.Id);
        b.Property(x => x.TrackingStatus).HasDefaultValue(ContainerTrackingStatus.Booked);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.ContainerNumber).HasMaxLength(11); b.HasIndex(x => x.ContainerNumber).IsUnique().HasFilter("\"containerNumber\" IS NOT NULL AND \"status\" = 1");
        b.Property(x => x.ContainerType).HasMaxLength(20).IsRequired(); b.Property(x => x.Cargo).HasMaxLength(180).IsRequired();
        b.Property(x => x.WeightKg).HasPrecision(14, 3); b.Property(x => x.Reference).HasMaxLength(60); b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne(x => x.Shipper).WithMany().HasForeignKey(x => x.ShipperId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Consignee).WithMany().HasForeignKey(x => x.ConsigneeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OriginPort).WithMany().HasForeignKey(x => x.OriginPortId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DestinationPort).WithMany().HasForeignKey(x => x.DestinationPortId).OnDelete(DeleteBehavior.Restrict); AuditConfiguration.Configure(b);
    }
}
public sealed class ContainerAttachmentConfiguration : IEntityTypeConfiguration<ContainerAttachment>
{
    public void Configure(EntityTypeBuilder<ContainerAttachment> b)
    {
        b.ToTable("containerAttachment"); b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(200).IsRequired(); b.Property(x => x.RelativePath).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired(); b.HasIndex(x => x.RelativePath).IsUnique();
        b.HasOne(x => x.Registration).WithMany(x => x.Attachments).HasForeignKey(x => x.ContainerRegistrationId).OnDelete(DeleteBehavior.Restrict); AuditConfiguration.Configure(b);
    }
}
