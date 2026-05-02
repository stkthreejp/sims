using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("invoice_lines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FeeCode).HasMaxLength(50).IsRequired();
        builder.Property(e => e.FeeDisplayName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FeeCategory).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Amount).HasColumnType("numeric(19,4)");
        builder.Property(e => e.PayableRouting).HasMaxLength(50);

        builder.HasOne(e => e.LedgerAccount)
            .WithMany()
            .HasForeignKey(e => e.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.InvoiceId);
    }
}
