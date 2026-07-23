using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyNumberSequenceConfiguration : IEntityTypeConfiguration<PolicyNumberSequence>
{
    public void Configure(EntityTypeBuilder<PolicyNumberSequence> builder)
    {
        builder.ToTable("policy_number_sequences");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(160);
        builder.Property(s => s.Format).IsRequired().HasMaxLength(100);
        builder.Property(s => s.TermSuffixFormat).IsRequired().HasMaxLength(30);
        builder.Property(s => s.Notes).HasMaxLength(1000);
        // Exclude soft-deleted rows so a deleted sequence's name can be reused (WS5-R Batch 2, A2.1).
        builder.HasIndex(s => s.Name).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}
