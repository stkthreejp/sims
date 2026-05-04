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

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted");
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");

        builder.Property(t => t.TerritoryNumber).IsRequired().HasColumnName("territory_number");
        builder.Property(t => t.States).IsRequired().HasMaxLength(200).HasColumnName("states");
        builder.Property(t => t.Modifier).HasPrecision(8, 6).HasColumnName("modifier");
        builder.HasIndex(t => t.TerritoryNumber).IsUnique();
    }
}
