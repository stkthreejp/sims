using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ProgramConfigurationConfiguration : IEntityTypeConfiguration<ProgramConfiguration>
{
    public void Configure(EntityTypeBuilder<ProgramConfiguration> builder)
    {
        builder.ToTable("program_configurations");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(160);
        builder.Property(p => p.Code).IsRequired().HasMaxLength(60);
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.HasIndex(p => p.Code).IsUnique();
        builder.HasIndex(p => p.IsActive);
    }
}
