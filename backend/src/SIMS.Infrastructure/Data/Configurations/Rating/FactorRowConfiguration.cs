using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class FactorRowConfiguration : IEntityTypeConfiguration<FactorRow>
{
    public void Configure(EntityTypeBuilder<FactorRow> builder)
    {
        builder.ToTable("factor_rows");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Factor).HasPrecision(18, 6);

        builder.Property(r => r.DimensionValues)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("jsonb")
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>>(
                (a, b) => a != null && b != null && a.Count == b.Count && !a.Except(b).Any(),
                v => v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
                v => new Dictionary<string, string>(v)));

        builder.HasIndex(r => r.DimensionValues)
            .HasMethod("gin")
            .HasAnnotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });
    }
}
