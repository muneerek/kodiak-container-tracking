using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;
public sealed class TimeZoneConfiguration : IEntityTypeConfiguration<TimeZoneMaster>
{
    public void Configure(EntityTypeBuilder<TimeZoneMaster> b)
    {
        b.ToTable("timeZone"); b.HasKey(x => x.Name);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Abbreviation).HasMaxLength(50).IsRequired();
    }
}
