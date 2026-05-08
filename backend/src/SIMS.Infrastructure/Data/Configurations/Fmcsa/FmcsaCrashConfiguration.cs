using SIMS.Domain.Entities.Fmcsa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations.Fmcsa;

public class FmcsaCrashConfiguration : IEntityTypeConfiguration<FmcsaCrash>
{
    public void Configure(EntityTypeBuilder<FmcsaCrash> builder)
    {
        builder.ToTable("fmcsa_crashes");
        builder.HasKey(x => x.Id);
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UsDotNumber).HasColumnName("us_dot_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReportNumber).HasColumnName("report_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.CrashDate).HasColumnName("crash_date");
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(2);
        builder.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
        builder.Property(x => x.CountyCode).HasColumnName("county_code").HasMaxLength(20);
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(200);
        builder.Property(x => x.Agency).HasColumnName("agency").HasMaxLength(150);
        builder.Property(x => x.VehiclesInAccident).HasColumnName("vehicles_in_accident");
        builder.Property(x => x.WeatherConditionId).HasColumnName("weather_condition_id").HasMaxLength(20);
        builder.Property(x => x.RoadSurfaceConditionId).HasColumnName("road_surface_condition_id").HasMaxLength(20);
        builder.Property(x => x.TrafficwayId).HasColumnName("trafficway_id").HasMaxLength(20);
        builder.Property(x => x.LightConditionId).HasColumnName("light_condition_id").HasMaxLength(20);
        builder.Property(x => x.VehicleConfigurationId).HasColumnName("vehicle_configuration_id").HasMaxLength(20);
        builder.Property(x => x.CargoBodyTypeId).HasColumnName("cargo_body_type_id").HasMaxLength(20);
        builder.Property(x => x.GvwRatingId).HasColumnName("gvw_rating_id").HasMaxLength(20);
        builder.Property(x => x.VehicleIdentificationNumber).HasColumnName("vehicle_identification_number").HasMaxLength(50);
        builder.Property(x => x.VehicleLicenseNumber).HasColumnName("vehicle_license_number").HasMaxLength(50);
        builder.Property(x => x.VehicleLicenseState).HasColumnName("vehicle_license_state").HasMaxLength(2);
        builder.Property(x => x.HazmatPlacard).HasColumnName("hazmat_placard");
        builder.Property(x => x.HazmatReleased).HasColumnName("hazmat_released");
        builder.Property(x => x.TowAway).HasColumnName("tow_away");
        builder.Property(x => x.Injury).HasColumnName("injury");
        builder.Property(x => x.Fatality).HasColumnName("fatality");
        builder.Property(x => x.SeverityWeight).HasColumnName("severity_weight").HasPrecision(8, 4);
        builder.Property(x => x.TimeWeight).HasColumnName("time_weight").HasPrecision(8, 4);
        builder.Property(x => x.ImportedAt).HasColumnName("imported_at");
        builder.HasIndex(x => new { x.UsDotNumber, x.CrashDate });
        builder.HasIndex(x => new { x.UsDotNumber, x.ReportNumber }).IsUnique();
    }
}
