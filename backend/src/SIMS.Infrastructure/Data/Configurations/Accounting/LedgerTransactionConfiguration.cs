using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> b)
    {
        b.ToTable("ledger_transactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Debit).HasColumnType("numeric(19,4)");
        b.Property(x => x.Credit).HasColumnType("numeric(19,4)");
        b.Property(x => x.SourceType).IsRequired().HasMaxLength(50);
        b.Property(x => x.PostingStatus).IsRequired().HasMaxLength(20);
        b.Property(x => x.VoidReason).HasMaxLength(500);
        b.Property(x => x.Memo).HasMaxLength(500);

        b.HasOne(x => x.Account)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Rollup)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.RolledUpIn)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.TransactionId).HasDatabaseName("ix_ledger_txn_id");
        b.HasIndex(x => x.AccountId).HasDatabaseName("ix_ledger_account");
        // Partial index for unrolled rows handled in the trigger migration
    }
}
