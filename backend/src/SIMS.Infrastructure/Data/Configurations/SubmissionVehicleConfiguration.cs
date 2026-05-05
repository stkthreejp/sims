using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

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

        // APD rating columns — explicit snake_case to avoid EF quoting PascalCase
        builder.Property(v => v.ApdVehicleClass).HasColumnName("apd_vehicle_class");
        builder.Property(v => v.ApdRoadType).HasColumnName("apd_road_type");
        builder.Property(v => v.ApdAnnualMiles).HasColumnName("apd_annual_miles");
        builder.Property(v => v.ApdOperationCode).HasColumnName("apd_operation_code");
        builder.Property(v => v.ApdState).HasColumnName("apd_state").HasMaxLength(2);
        builder.Property(v => v.ApdStatedValue).HasColumnName("apd_stated_value").HasPrecision(18, 2);
        builder.Property(v => v.ApdCompDeductible).HasColumnName("apd_comp_deductible").HasPrecision(18, 2);
        builder.Property(v => v.ApdCollDeductible).HasColumnName("apd_coll_deductible").HasPrecision(18, 2);
        builder.Property(v => v.ApdDriverAgeCode).HasColumnName("apd_driver_age_code");
        builder.Property(v => v.ApdDriverPointsCode).HasColumnName("apd_driver_points_code");
        builder.Property(v => v.ApdDriverExpMod).HasColumnName("apd_driver_exp_mod").HasPrecision(5, 2);

        builder.HasOne(v => v.Submission).WithMany(s => s.Vehicles)
            .HasForeignKey(v => v.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
