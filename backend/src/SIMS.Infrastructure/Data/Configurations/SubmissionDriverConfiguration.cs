using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class SubmissionDriverConfiguration : IEntityTypeConfiguration<SubmissionDriver>
{
    public void Configure(EntityTypeBuilder<SubmissionDriver> builder)
    {
        builder.ToTable("submission_drivers");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.Property(d => d.LicenseNumber).HasMaxLength(50);
        builder.Property(d => d.LicenseState).HasMaxLength(2);

        builder.HasOne(d => d.Submission).WithMany(s => s.Drivers)
            .HasForeignKey(d => d.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
