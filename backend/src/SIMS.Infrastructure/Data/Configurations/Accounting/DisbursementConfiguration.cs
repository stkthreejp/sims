using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class DisbursementConfiguration : IEntityTypeConfiguration<Disbursement>
{
    public void Configure(EntityTypeBuilder<Disbursement> builder)
    {
        builder.ToTable("disbursements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DisbursementNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PayeeName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.TotalAmount).HasColumnType("numeric(19,4)");
        builder.Property(e => e.PaymentMethod).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.Property(e => e.Status).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasIndex(e => e.DisbursementNumber).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CarrierId);
    }
}
