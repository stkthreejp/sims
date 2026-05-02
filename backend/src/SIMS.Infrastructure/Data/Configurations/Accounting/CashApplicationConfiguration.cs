using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class CashApplicationConfiguration : IEntityTypeConfiguration<CashApplication>
{
    public void Configure(EntityTypeBuilder<CashApplication> builder)
    {
        builder.ToTable("cash_applications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.GrossApplied).HasColumnType("numeric(19,4)");
        builder.Property(e => e.CommissionAmount).HasColumnType("numeric(19,4)");
        builder.Property(e => e.NetApplied).HasColumnType("numeric(19,4)");

        builder.HasOne(e => e.Invoice)
            .WithMany()
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.LedgerTransactionId);
        builder.HasIndex(e => new { e.ReceiptId, e.InvoiceId });
    }
}
