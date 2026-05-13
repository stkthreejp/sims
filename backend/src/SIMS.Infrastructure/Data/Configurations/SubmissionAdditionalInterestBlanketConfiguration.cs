using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class SubmissionAdditionalInterestBlanketConfiguration : IEntityTypeConfiguration<SubmissionAdditionalInterestBlanket>
{
    public void Configure(EntityTypeBuilder<SubmissionAdditionalInterestBlanket> builder)
    {
        builder.ToTable("submission_additional_interest_blankets");
        builder.HasKey(b => b.Id);

        builder.HasIndex(b => new { b.SubmissionId, b.LineOfBusiness, b.IsDeleted });

        builder.HasOne(b => b.Submission).WithMany(s => s.AdditionalInterestBlankets)
            .HasForeignKey(b => b.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
