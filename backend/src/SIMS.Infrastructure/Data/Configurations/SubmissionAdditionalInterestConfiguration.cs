using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class SubmissionAdditionalInterestConfiguration : IEntityTypeConfiguration<SubmissionAdditionalInterest>
{
    public void Configure(EntityTypeBuilder<SubmissionAdditionalInterest> builder)
    {
        builder.ToTable("submission_additional_interests");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(250);
        builder.Property(a => a.AddressLine1).HasMaxLength(250);
        builder.Property(a => a.AddressLine2).HasMaxLength(250);
        builder.Property(a => a.City).HasMaxLength(100);
        builder.Property(a => a.State).HasMaxLength(2);
        builder.Property(a => a.ZipCode).HasMaxLength(20);
        builder.Property(a => a.Email).HasMaxLength(320);
        builder.Property(a => a.Phone).HasMaxLength(50);
        builder.Property(a => a.ScheduledItemNumbers).HasMaxLength(500);
        builder.Property(a => a.Notes).HasMaxLength(1000);

        builder.HasIndex(a => new { a.SubmissionId, a.LineOfBusiness, a.IsDeleted });

        builder.HasOne(a => a.Submission).WithMany(s => s.AdditionalInterests)
            .HasForeignKey(a => a.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
