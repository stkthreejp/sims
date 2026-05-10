using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class QuoteChecklistItemConfiguration : IEntityTypeConfiguration<QuoteChecklistItem>
{
    public void Configure(EntityTypeBuilder<QuoteChecklistItem> builder)
    {
        builder.ToTable("quote_checklist_items");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TriggerKey).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Label).IsRequired().HasMaxLength(200);
        builder.Property(c => c.CompletionSource).IsRequired().HasMaxLength(20);
        builder.Property(c => c.CompletedByName).HasMaxLength(200);

        builder.HasOne(c => c.Quote).WithMany()
            .HasForeignKey(c => c.QuoteId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.CompletedBy).WithMany()
            .HasForeignKey(c => c.CompletedById).OnDelete(DeleteBehavior.SetNull);
    }
}
