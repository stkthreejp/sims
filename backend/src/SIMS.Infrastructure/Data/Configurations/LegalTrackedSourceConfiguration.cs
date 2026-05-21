using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class LegalTrackedSourceConfiguration : IEntityTypeConfiguration<LegalTrackedSource>
{
    public void Configure(EntityTypeBuilder<LegalTrackedSource> builder)
    {
        builder.ToTable("legal_tracked_sources");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.State).IsRequired().HasMaxLength(80);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(160);
        builder.Property(s => s.SourceType).IsRequired().HasMaxLength(80);
        builder.Property(s => s.Url).HasMaxLength(1000);
        builder.Property(s => s.ApiKey).HasMaxLength(1000);
        builder.Property(s => s.ScanCadence).IsRequired().HasMaxLength(40);
        builder.Property(s => s.LastStatus).IsRequired().HasMaxLength(40);
        builder.Property(s => s.LastErrorMessage).HasMaxLength(2000);
        builder.Property(s => s.Notes).HasMaxLength(2000);

        builder.HasIndex(s => new { s.State, s.Name, s.SourceType }).IsUnique();
        builder.HasIndex(s => s.IsEnabled);
        builder.HasIndex(s => s.LastCheckedAt);
    }
}
