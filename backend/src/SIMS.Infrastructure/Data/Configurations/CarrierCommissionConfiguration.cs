using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class CarrierCommissionConfiguration : IEntityTypeConfiguration<CarrierCommission>
{
    public void Configure(EntityTypeBuilder<CarrierCommission> builder)
    {
        builder.ToTable("carrier_commissions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CommissionRate).HasColumnType("numeric(8,6)");
        builder.Property(e => e.LineOfBusiness).HasMaxLength(50);

        builder.HasOne(e => e.ProgramConfiguration)
            .WithMany()
            .HasForeignKey(e => e.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Carrier)
            .WithMany()
            .HasForeignKey(e => e.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProgramConfigurationId, e.CarrierId, e.LineOfBusiness, e.EffectiveDate })
            .IsUnique()
            .AreNullsDistinct(false);
        builder.HasIndex(e => new { e.CarrierId, e.DisabledDate });
    }
}
