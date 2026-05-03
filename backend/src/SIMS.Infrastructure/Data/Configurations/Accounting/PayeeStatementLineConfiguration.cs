using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class PayeeStatementLineConfiguration : IEntityTypeConfiguration<PayeeStatementLine>
{
    public void Configure(EntityTypeBuilder<PayeeStatementLine> builder)
    {
        builder.ToTable("payee_statement_lines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PolicyNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.StateCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Amount).HasColumnType("numeric(19,4)");
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.MatchStatus).HasMaxLength(20).IsRequired();

        builder.HasOne(e => e.MatchedInvoiceLine)
            .WithMany()
            .HasForeignKey(e => e.MatchedInvoiceLineId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.PayeeStatementId);
        builder.HasIndex(e => e.MatchStatus);
        builder.HasIndex(e => e.MatchedInvoiceLineId);
    }
}
