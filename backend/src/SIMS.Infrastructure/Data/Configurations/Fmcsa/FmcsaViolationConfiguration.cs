using SIMS.Domain.Entities.Fmcsa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations.Fmcsa;

public class FmcsaViolationConfiguration : IEntityTypeConfiguration<FmcsaViolation>
{
    public void Configure(EntityTypeBuilder<FmcsaViolation> builder)
    {
        builder.ToTable("fmcsa_violations");
        builder.HasKey(x => x.Id);
        builder.ConfigureBaseEntity();
        builder.Property(x => x.FmcsaInspectionId).HasColumnName("fmcsa_inspection_id");
        builder.Property(x => x.UsDotNumber).HasColumnName("us_dot_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReportNumber).HasColumnName("report_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ViolationCode).HasColumnName("violation_code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(x => x.Basic).HasColumnName("basic").HasMaxLength(80);
        builder.Property(x => x.ViolationGroup).HasColumnName("violation_group").HasMaxLength(120);
        builder.Property(x => x.UnitNumber).HasColumnName("unit_number").HasMaxLength(20);
        builder.Property(x => x.OosWeight).HasColumnName("oos_weight").HasPrecision(8, 4);
        builder.Property(x => x.IsOutOfService).HasColumnName("is_out_of_service");
        builder.Property(x => x.IsDriverDisqualifying).HasColumnName("is_driver_disqualifying");
        builder.Property(x => x.SeverityWeight).HasColumnName("severity_weight");
        builder.Property(x => x.TimeWeight).HasColumnName("time_weight").HasPrecision(8, 4);
        builder.Property(x => x.ImportedAt).HasColumnName("imported_at");
        builder.HasIndex(x => new { x.UsDotNumber, x.ReportNumber });
        builder.HasOne(x => x.Inspection).WithMany(x => x.Violations)
            .HasForeignKey(x => x.FmcsaInspectionId).OnDelete(DeleteBehavior.Cascade);
    }
}
