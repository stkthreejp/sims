using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class JournalEntryRollupConfiguration : IEntityTypeConfiguration<JournalEntryRollup>
{
    public void Configure(EntityTypeBuilder<JournalEntryRollup> b)
    {
        b.ToTable("journal_entry_rollups");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.DriverType).IsRequired().HasMaxLength(20);
        b.Property(x => x.Status).IsRequired().HasMaxLength(20);
        b.Property(x => x.ExternalId).HasMaxLength(100);
        b.Property(x => x.BlobUri).HasMaxLength(500);
    }
}
