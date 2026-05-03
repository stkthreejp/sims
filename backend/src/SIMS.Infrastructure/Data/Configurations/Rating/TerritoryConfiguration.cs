using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class TerritoryConfiguration : IEntityTypeConfiguration<Territory>
{
    public void Configure(EntityTypeBuilder<Territory> builder)
    {
        builder.ToTable("territories");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TerritoryNumber).IsRequired();
        builder.Property(t => t.States).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Modifier).HasPrecision(8, 6);
        builder.HasIndex(t => t.TerritoryNumber).IsUnique();
    }
}
