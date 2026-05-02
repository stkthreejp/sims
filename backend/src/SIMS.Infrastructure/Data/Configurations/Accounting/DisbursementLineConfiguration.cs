using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class DisbursementLineConfiguration : IEntityTypeConfiguration<DisbursementLine>
{
    public void Configure(EntityTypeBuilder<DisbursementLine> builder)
    {
        builder.ToTable("disbursement_lines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).HasColumnType("numeric(19,4)");

        builder.HasOne(e => e.Disbursement)
            .WithMany(d => d.Lines)
            .HasForeignKey(e => e.DisbursementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Payable)
            .WithMany(p => p.DisbursementLines)
            .HasForeignKey(e => e.PayableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.DisbursementId);
        builder.HasIndex(e => e.PayableId);
    }
}
