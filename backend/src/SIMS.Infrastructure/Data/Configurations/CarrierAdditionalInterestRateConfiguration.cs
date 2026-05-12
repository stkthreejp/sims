using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class CarrierAdditionalInterestRateConfiguration : IEntityTypeConfiguration<CarrierAdditionalInterestRate>
{
    public void Configure(EntityTypeBuilder<CarrierAdditionalInterestRate> builder)
    {
        builder.ToTable("carrier_additional_interest_rates");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.PerInterestAmount).HasPrecision(18, 2);
        builder.Property(r => r.BlanketAmount).HasPrecision(18, 2);
        builder.Property(r => r.MinimumCharge).HasPrecision(18, 2);
        builder.Property(r => r.MaximumCharge).HasPrecision(18, 2);
        builder.Property(r => r.State).HasMaxLength(2);

        builder.HasIndex(r => new { r.CarrierId, r.LineOfBusiness, r.CoverageType, r.IsActive, r.IsDeleted });

        builder.HasOne(r => r.Carrier).WithMany()
            .HasForeignKey(r => r.CarrierId).OnDelete(DeleteBehavior.Cascade);
    }
}
