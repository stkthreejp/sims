using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Data.Configurations;

public class SubmissionPriorCarrierConfiguration : IEntityTypeConfiguration<SubmissionPriorCarrier>
{
    public void Configure(EntityTypeBuilder<SubmissionPriorCarrier> builder)
    {
        builder.ToTable("submission_prior_carriers");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.CarrierName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.PolicyNumber).HasMaxLength(100);
        builder.Property(p => p.Premium).HasPrecision(18, 2);

        builder.HasOne(p => p.Submission).WithMany(s => s.PriorCarriers)
            .HasForeignKey(p => p.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
