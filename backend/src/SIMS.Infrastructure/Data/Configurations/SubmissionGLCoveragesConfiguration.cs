using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class SubmissionGLCoveragesConfiguration : IEntityTypeConfiguration<SubmissionGLCoverages>
{
    public void Configure(EntityTypeBuilder<SubmissionGLCoverages> builder)
    {
        builder.ToTable("submission_gl_coverages");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.GeneralAggregate).HasPrecision(18, 2);
        builder.Property(g => g.ProductsCompletedOps).HasPrecision(18, 2);
        builder.Property(g => g.EachOccurrence).HasPrecision(18, 2);
        builder.Property(g => g.PersonalAndAdvInjury).HasPrecision(18, 2);
        builder.Property(g => g.DamageToRentedPremises).HasPrecision(18, 2);
        builder.Property(g => g.MedicalExpense).HasPrecision(18, 2);
        builder.Property(g => g.TotalSubcontractorCost).HasPrecision(18, 2);

        builder.Property(g => g.AiIndividualCount).HasColumnName("ai_individual_count").HasDefaultValue(0);
        builder.Property(g => g.AiBlanket).HasColumnName("ai_blanket").HasDefaultValue(false);
        builder.Property(g => g.WosIndividualCount).HasColumnName("wos_individual_count").HasDefaultValue(0);
        builder.Property(g => g.WosBlanket).HasColumnName("wos_blanket").HasDefaultValue(false);
        builder.Property(g => g.PrimaryNonContributory).HasColumnName("primary_non_contributory").HasDefaultValue(false);
        builder.Property(g => g.IncludeTria).HasColumnName("include_tria").HasDefaultValue(false);
        builder.Property(g => g.LoggingLumberingLimit).HasColumnName("logging_lumbering_limit").HasPrecision(18, 2);

        builder.HasOne(g => g.Submission).WithOne(s => s.GLCoverages)
            .HasForeignKey<SubmissionGLCoverages>(g => g.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
