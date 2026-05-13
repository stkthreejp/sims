using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class SubmissionIMCoveragesConfiguration : IEntityTypeConfiguration<SubmissionIMCoverages>
{
    public void Configure(EntityTypeBuilder<SubmissionIMCoverages> builder)
    {
        builder.ToTable("submission_im_coverages");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.ScheduledEquipmentTotalLimit).HasPrecision(18, 2);
        builder.Property(i => i.UnscheduledEquipmentLimit).HasPrecision(18, 2);
        builder.Property(i => i.MaximumValueAnyOneItem).HasPrecision(18, 2);
        builder.Property(i => i.Deductible).HasPrecision(18, 2);
        builder.Property(i => i.CoinsurancePercentage).HasPrecision(7, 4);

        builder.HasOne(i => i.Submission).WithOne(s => s.IMCoverages)
            .HasForeignKey<SubmissionIMCoverages>(i => i.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
