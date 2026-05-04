using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class FactorTableConfiguration : IEntityTypeConfiguration<FactorTable>
{
    public void Configure(EntityTypeBuilder<FactorTable> builder)
    {
        builder.ToTable("factor_tables");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted");
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");

        builder.Property(t => t.RatingPlanVersionId).HasColumnName("rating_plan_version_id");
        builder.Property(t => t.Code).IsRequired().HasMaxLength(50).HasColumnName("code");
        builder.Property(t => t.ValueSemantics).HasColumnName("value_semantics");

        builder.Property(t => t.DimensionNames)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>())
            .HasColumnType("jsonb")
            .HasColumnName("dimension_names");
        builder.Property(t => t.DimensionNames).Metadata.SetValueComparer(new ValueComparer<string[]>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToArray()));

        builder.HasMany(t => t.Rows).WithOne(r => r.FactorTable)
            .HasForeignKey(r => r.FactorTableId).OnDelete(DeleteBehavior.Cascade);
    }
}
