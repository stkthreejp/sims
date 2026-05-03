using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class PeriodCloseChecklistItemConfiguration : IEntityTypeConfiguration<PeriodCloseChecklistItem>
{
    public void Configure(EntityTypeBuilder<PeriodCloseChecklistItem> b)
    {
        b.ToTable("period_close_checklist");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.CheckKey).IsRequired().HasMaxLength(30);

        b.HasOne(x => x.Period)
            .WithMany()
            .HasForeignKey(x => x.PeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.PeriodId, x.CheckKey }).IsUnique();
    }
}
