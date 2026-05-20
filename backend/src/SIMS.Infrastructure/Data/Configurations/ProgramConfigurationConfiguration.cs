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
        builder.Property(p => p.StateCode).IsRequired().HasMaxLength(3);
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.HasIndex(p => p.Code).IsUnique();
        builder.HasIndex(p => new { p.CarrierId, p.LineOfBusiness, p.StateCode, p.IsActive });

        builder.HasOne(p => p.Carrier)
            .WithMany()
            .HasForeignKey(p => p.CarrierId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
