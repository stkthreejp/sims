using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Data.Configurations;

public class SubmissionSupplementalConfiguration : IEntityTypeConfiguration<SubmissionSupplemental>
{
    public void Configure(EntityTypeBuilder<SubmissionSupplemental> builder)
    {
        builder.ToTable("submission_supplementals");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.CommoditiesHauled).HasMaxLength(2000);
        builder.Property(s => s.TerminalLocations).HasMaxLength(2000);
        builder.Property(s => s.FilingsRequired).HasMaxLength(500);

        builder.HasOne(s => s.Submission).WithOne(sub => sub.Supplemental)
            .HasForeignKey<SubmissionSupplemental>(s => s.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
