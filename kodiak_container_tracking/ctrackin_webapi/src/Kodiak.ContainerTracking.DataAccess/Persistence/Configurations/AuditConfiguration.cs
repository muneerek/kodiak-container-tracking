using Kodiak.ContainerTracking.DataAccess.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kodiak.ContainerTracking.DataAccess.Persistence.Configurations;
internal static class AuditConfiguration
{
    internal static void Configure<T>(EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        builder.Property(x => x.CreatedBy).HasMaxLength(50);
        builder.Property(x => x.UpdatedBy).HasMaxLength(50);
    }
}
