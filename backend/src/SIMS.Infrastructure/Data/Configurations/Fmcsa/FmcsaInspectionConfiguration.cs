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
        builder.Property(x => x.ExternalInspectionId).HasColumnName("external_inspection_id").HasMaxLength(20);
        builder.Property(x => x.ReportNumber).HasColumnName("report_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.InspectionDate).HasColumnName("inspection_date");
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(2);
        builder.Property(x => x.CountyCodeState).HasColumnName("county_code_state").HasMaxLength(50);
        builder.Property(x => x.CountyCode).HasColumnName("county_code").HasMaxLength(10);
        builder.Property(x => x.InspectionCounty).HasColumnName("inspection_county").HasMaxLength(100);
        builder.Property(x => x.InspectionLocation).HasColumnName("inspection_location").HasMaxLength(200);
        builder.Property(x => x.InspectionFacility).HasColumnName("inspection_facility").HasMaxLength(50);
        builder.Property(x => x.StartTime).HasColumnName("start_time").HasMaxLength(20);
        builder.Property(x => x.EndTime).HasColumnName("end_time").HasMaxLength(20);
        builder.Property(x => x.PostCrash).HasColumnName("post_crash");
        builder.Property(x => x.HazmatPlacardRequired).HasColumnName("hazmat_placard_required");
        builder.Property(x => x.InspectionLevelDescription).HasColumnName("inspection_level_description").HasMaxLength(100);
        builder.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(9, 6);
        builder.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(9, 6);
        builder.Property(x => x.GeocodePrecision).HasColumnName("geocode_precision").HasMaxLength(50);
        builder.Property(x => x.DetailSourceUrl).HasColumnName("detail_source_url").HasMaxLength(500);
        builder.Property(x => x.DetailEnrichedAt).HasColumnName("detail_enriched_at");
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
        builder.HasIndex(x => new { x.UsDotNumber, x.ExternalInspectionId });
        builder.HasIndex(x => new { x.UsDotNumber, x.ReportNumber }).IsUnique();
    }
}
