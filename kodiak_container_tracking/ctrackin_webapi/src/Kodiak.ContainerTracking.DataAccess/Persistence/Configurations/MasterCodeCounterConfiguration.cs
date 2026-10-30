using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;
public sealed class MasterCodeCounterConfiguration : IEntityTypeConfiguration<MasterCodeCounter>
{
    public void Configure(EntityTypeBuilder<MasterCodeCounter> b)
    {
        b.ToTable("masterCodeCounters"); b.HasKey(x => x.Kind);
        b.Property(x => x.Kind).HasMaxLength(50);
        b.HasData(new MasterCodeCounter { Kind = "ShippingPorts", LastNumber = 0 });
    }
}
