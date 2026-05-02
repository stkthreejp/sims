using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class CashMovementInstructionConfiguration : IEntityTypeConfiguration<CashMovementInstruction>
{
    public void Configure(EntityTypeBuilder<CashMovementInstruction> builder)
    {
        builder.ToTable("cash_movement_instructions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).HasColumnType("numeric(19,4)");
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();

        builder.HasOne(e => e.Receipt)
            .WithMany()
            .HasForeignKey(e => e.ReceiptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CashApplication)
            .WithMany()
            .HasForeignKey(e => e.CashApplicationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.InvoiceLine)
            .WithMany()
            .HasForeignKey(e => e.InvoiceLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Payee)
            .WithMany()
            .HasForeignKey(e => e.PayeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Batch)
            .WithMany(b => b.Instructions)
            .HasForeignKey(e => e.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ReceiptId);
        builder.HasIndex(e => e.CashApplicationId);
        builder.HasIndex(e => e.BatchId);
    }
}
