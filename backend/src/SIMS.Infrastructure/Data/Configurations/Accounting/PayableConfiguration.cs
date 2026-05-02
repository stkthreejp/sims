using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class PayableConfiguration : IEntityTypeConfiguration<Payable>
{
    public void Configure(EntityTypeBuilder<Payable> builder)
    {
        builder.ToTable("payables");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PayeeName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Amount).HasColumnType("numeric(19,4)");
        builder.Property(e => e.PaidAmount).HasColumnType("numeric(19,4)");
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();

        builder.HasOne(e => e.Invoice)
            .WithMany()
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.GlAccount)
            .WithMany()
            .HasForeignKey(e => e.GlAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.InvoiceId);
        builder.HasIndex(e => e.CarrierId);
        builder.HasIndex(e => e.DueDate);
    }
}
