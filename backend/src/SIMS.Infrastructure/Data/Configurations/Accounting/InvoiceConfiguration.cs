using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.GrossPremium).HasColumnType("numeric(19,4)");
        builder.Property(e => e.TotalFees).HasColumnType("numeric(19,4)");
        builder.Property(e => e.TotalAmount).HasColumnType("numeric(19,4)");
        builder.Property(e => e.ClearedAmount).HasColumnType("numeric(19,4)").HasDefaultValue(0m);

        builder.HasMany(e => e.Lines)
            .WithOne(l => l.Invoice)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.InvoiceNumber).IsUnique();
        builder.HasIndex(e => e.LedgerTransactionId);
        builder.HasIndex(e => new { e.TenantId, e.InvoiceDate });
    }
}
