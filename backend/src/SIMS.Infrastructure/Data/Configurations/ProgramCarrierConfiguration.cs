using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ProgramCarrierConfiguration : IEntityTypeConfiguration<ProgramCarrier>
{
    public void Configure(EntityTypeBuilder<ProgramCarrier> builder)
    {
        builder.ToTable("program_carriers");

        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => new { x.ProgramConfigurationId, x.CarrierId }).IsUnique();
        builder.HasIndex(x => x.IsActive);

        builder.HasOne(x => x.ProgramConfiguration)
            .WithMany(p => p.ProgramCarriers)
            .HasForeignKey(x => x.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Carrier)
            .WithMany()
            .HasForeignKey(x => x.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
