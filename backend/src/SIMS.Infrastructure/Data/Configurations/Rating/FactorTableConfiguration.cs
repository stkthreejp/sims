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
        builder.Property(t => t.Code).IsRequired().HasMaxLength(50);

        builder.Property(t => t.DimensionNames)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>())
            .HasColumnType("jsonb")
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                v => v.ToArray()));

        builder.HasMany(t => t.Rows).WithOne(r => r.FactorTable)
            .HasForeignKey(r => r.FactorTableId).OnDelete(DeleteBehavior.Cascade);
    }
}
