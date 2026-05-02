using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class CashDistributionBatchConfiguration : IEntityTypeConfiguration<CashDistributionBatch>
{
    public void Configure(EntityTypeBuilder<CashDistributionBatch> builder)
    {
        builder.ToTable("cash_distribution_batches");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.TotalAmount).HasColumnType("numeric(19,4)");
        builder.Property(e => e.PdfBlobPath).HasMaxLength(500);
        builder.Property(e => e.BankReference).HasMaxLength(100);

        builder.HasIndex(e => e.BatchNumber).IsUnique();
        builder.HasIndex(e => e.Status);
    }
}
