using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class QuotePolicyFormSelectionConfiguration : IEntityTypeConfiguration<QuotePolicyFormSelection>
{
    public void Configure(EntityTypeBuilder<QuotePolicyFormSelection> builder)
    {
        builder.ToTable("quote_policy_form_selections");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.TriggerConditionJson).HasColumnType("jsonb");
        builder.Property(f => f.Notes).HasMaxLength(1000);

        builder.HasIndex(f => new { f.QuoteId, f.SequenceOrder, f.IsDeleted });
        builder.HasIndex(f => new { f.QuoteId, f.PolicyFormTemplateId, f.IsDeleted });

        builder.HasOne(f => f.Quote)
            .WithMany()
            .HasForeignKey(f => f.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.PolicyFormTemplate)
            .WithMany()
            .HasForeignKey(f => f.PolicyFormTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
