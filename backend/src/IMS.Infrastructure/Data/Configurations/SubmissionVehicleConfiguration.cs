using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Data.Configurations;

public class SubmissionVehicleConfiguration : IEntityTypeConfiguration<SubmissionVehicle>
{
    public void Configure(EntityTypeBuilder<SubmissionVehicle> builder)
    {
        builder.ToTable("submission_vehicles");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Make).HasMaxLength(100);
        builder.Property(v => v.Model).HasMaxLength(100);
        builder.Property(v => v.Vin).HasMaxLength(17);
        builder.Property(v => v.GaragingZip).HasMaxLength(10);

        builder.HasOne(v => v.Submission).WithMany(s => s.Vehicles)
            .HasForeignKey(v => v.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
