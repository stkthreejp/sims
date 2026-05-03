using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class PayeeStatementConfiguration : IEntityTypeConfiguration<PayeeStatement>
{
    public void Configure(EntityTypeBuilder<PayeeStatement> builder)
    {
        builder.ToTable("payee_statements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PayeeName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ReferenceNumber).HasMaxLength(100);
        builder.Property(e => e.BlobPath).HasMaxLength(500);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.StatementTotal).HasColumnType("numeric(19,4)");

        builder.HasOne(e => e.ApLedgerAccount)
            .WithMany()
            .HasForeignKey(e => e.ApLedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Lines)
            .WithOne(l => l.Statement)
            .HasForeignKey(l => l.PayeeStatementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.StatementDate);
    }
}
