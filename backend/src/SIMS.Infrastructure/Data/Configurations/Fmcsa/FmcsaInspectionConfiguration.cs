using SIMS.Domain.Entities.Fmcsa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations.Fmcsa;

public class FmcsaInspectionConfiguration : IEntityTypeConfiguration<FmcsaInspection>
{
    public void Configure(EntityTypeBuilder<FmcsaInspection> builder)
    {
        builder.ToTable("fmcsa_inspections");
        builder.HasKey(x => x.Id);
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UsDotNumber).HasColumnName("us_dot_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReportNumber).HasColumnName("report_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.InspectionDate).HasColumnName("inspection_date");
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(2);
        builder.Property(x => x.InspectionLevel).HasColumnName("inspection_level");
        builder.Property(x => x.DriverOutOfService).HasColumnName("driver_out_of_service");
        builder.Property(x => x.VehicleOutOfService).HasColumnName("vehicle_out_of_service");
        builder.Property(x => x.HazmatOutOfService).HasColumnName("hazmat_out_of_service");
        builder.Property(x => x.DriverViolationCount).HasColumnName("driver_violation_count");
        builder.Property(x => x.VehicleViolationCount).HasColumnName("vehicle_violation_count");
        builder.Property(x => x.HazmatViolationCount).HasColumnName("hazmat_violation_count");
        builder.Property(x => x.UnitType).HasColumnName("unit_type").HasMaxLength(100);
        builder.Property(x => x.UnitMake).HasColumnName("unit_make").HasMaxLength(100);
        builder.Property(x => x.UnitLicense).HasColumnName("unit_license").HasMaxLength(50);
        builder.Property(x => x.UnitLicenseState).HasColumnName("unit_license_state").HasMaxLength(2);
        builder.Property(x => x.Vin).HasColumnName("vin").HasMaxLength(50);
        builder.Property(x => x.UnitType2).HasColumnName("unit_type_2").HasMaxLength(100);
        builder.Property(x => x.UnitMake2).HasColumnName("unit_make_2").HasMaxLength(100);
        builder.Property(x => x.UnitLicense2).HasColumnName("unit_license_2").HasMaxLength(50);
        builder.Property(x => x.UnitLicenseState2).HasColumnName("unit_license_state_2").HasMaxLength(2);
        builder.Property(x => x.Vin2).HasColumnName("vin_2").HasMaxLength(50);
        builder.Property(x => x.ImportedAt).HasColumnName("imported_at");
        builder.HasIndex(x => new { x.UsDotNumber, x.InspectionDate });
        builder.HasIndex(x => new { x.UsDotNumber, x.ReportNumber }).IsUnique();
    }
}
