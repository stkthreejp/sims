using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Data.Configurations;

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

        builder.HasOne(g => g.Submission).WithOne(s => s.GLCoverages)
            .HasForeignKey<SubmissionGLCoverages>(g => g.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
