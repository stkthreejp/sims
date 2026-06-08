using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class SubmissionLocationConfiguration : IEntityTypeConfiguration<SubmissionLocation>
{
    public void Configure(EntityTypeBuilder<SubmissionLocation> builder)
    {
        builder.ToTable("submission_locations");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Address).IsRequired().HasMaxLength(300);
        builder.Property(l => l.City).HasMaxLength(100);
        builder.Property(l => l.State).HasMaxLength(2);
        builder.Property(l => l.County).HasMaxLength(100);
        builder.Property(l => l.ZipCode).HasMaxLength(20);
        builder.Property(l => l.Country).HasMaxLength(3);

        builder.HasOne(l => l.Submission).WithMany(s => s.Locations)
            .HasForeignKey(l => l.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
