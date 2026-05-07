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
        builder.Property(x => x.UsDotNumber).HasColumnName("us_dot_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReportNumber).HasColumnName("report_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.CrashDate).HasColumnName("crash_date");
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(2);
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
