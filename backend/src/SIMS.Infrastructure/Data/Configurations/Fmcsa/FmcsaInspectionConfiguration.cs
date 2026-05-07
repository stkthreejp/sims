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
        builder.Property(x => x.UsDotNumber).HasColumnName("us_dot_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReportNumber).HasColumnName("report_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.InspectionDate).HasColumnName("inspection_date");
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(2);
        builder.Property(x => x.InspectionLevel).HasColumnName("inspection_level");
        builder.Property(x => x.DriverOutOfService).HasColumnName("driver_out_of_service");
        builder.Property(x => x.VehicleOutOfService).HasColumnName("vehicle_out_of_service");
        builder.Property(x => x.DriverViolationCount).HasColumnName("driver_violation_count");
        builder.Property(x => x.VehicleViolationCount).HasColumnName("vehicle_violation_count");
        builder.Property(x => x.ImportedAt).HasColumnName("imported_at");
        builder.HasIndex(x => new { x.UsDotNumber, x.InspectionDate });
        builder.HasIndex(x => new { x.UsDotNumber, x.ReportNumber }).IsUnique();
    }
}
