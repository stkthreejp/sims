using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("receipts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ReceiptNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.PayerName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(200);
        builder.Property(e => e.RemittanceBlobPath).HasMaxLength(500);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Amount).HasColumnType("numeric(19,4)");
        builder.Property(e => e.AppliedAmount).HasColumnType("numeric(19,4)").HasDefaultValue(0m);

        builder.HasMany(e => e.Applications)
            .WithOne(a => a.Receipt)
            .HasForeignKey(a => a.ReceiptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ReceiptNumber).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.ReceivedDate });
    }
}
