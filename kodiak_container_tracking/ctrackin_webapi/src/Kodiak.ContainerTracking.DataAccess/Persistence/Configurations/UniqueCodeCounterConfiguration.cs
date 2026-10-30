using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;

public sealed class UniqueCodeCounterConfiguration : IEntityTypeConfiguration<UniqueCodeCounter>
{
    public void Configure(EntityTypeBuilder<UniqueCodeCounter> builder)
    {
        builder.ToTable("uniqueCodeCounters");
        builder.HasKey(x => x.PartyType);
        builder.Property(x => x.LastNumber).IsConcurrencyToken();
        builder.HasData(Enum.GetValues<PartyType>().Select(type => new UniqueCodeCounter { PartyType = type }));
    }
}
