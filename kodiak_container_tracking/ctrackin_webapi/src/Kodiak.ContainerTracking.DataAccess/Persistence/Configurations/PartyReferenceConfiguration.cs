using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;

public sealed class PartyReferenceConfiguration : IEntityTypeConfiguration<PartyReference>
{
    public void Configure(EntityTypeBuilder<PartyReference> builder)
    {
        builder.ToTable("partyReferences", t => t.HasCheckConstraint("ck_party_reference_type", "\"partyType\" IN (1, 2, 3)"));
        builder.HasKey(x => x.UniqueId);
        builder.HasAlternateKey(x => new { x.UniqueId, x.UniqueCode });
        builder.HasAlternateKey(x => new { x.UniqueId, x.PartyType, x.UniqueCode });
        builder.Property(x => x.UniqueCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.UniqueCode).IsUnique();
    }
}
