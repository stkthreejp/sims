using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ProgramCarrierLineOfBusinessConfiguration : IEntityTypeConfiguration<ProgramCarrierLineOfBusiness>
{
    public void Configure(EntityTypeBuilder<ProgramCarrierLineOfBusiness> builder)
    {
        builder.ToTable("program_carrier_lines_of_business");

        builder.Property(x => x.BillingMode).HasMaxLength(50);
        builder.Property(x => x.LondonUmr).HasMaxLength(120);
        builder.Property(x => x.LondonSectionNumber).HasMaxLength(80);
        builder.Property(x => x.LondonClassOfBusiness).HasMaxLength(160);
        builder.Property(x => x.LondonRiskCode).HasMaxLength(120);
        builder.Property(x => x.LondonInsuranceType).HasMaxLength(80);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => new { x.ProgramCarrierId, x.LineOfBusiness }).IsUnique();
        builder.HasIndex(x => x.IsActive);

        builder.HasOne(x => x.ProgramCarrier)
            .WithMany(c => c.LinesOfBusiness)
            .HasForeignKey(x => x.ProgramCarrierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
