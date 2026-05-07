using SIMS.Domain.Entities.Fmcsa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations.Fmcsa;

public class FmcsaBasicScoreConfiguration : IEntityTypeConfiguration<FmcsaBasicScore>
{
    public void Configure(EntityTypeBuilder<FmcsaBasicScore> builder)
    {
        builder.ToTable("fmcsa_basic_scores");
        builder.HasKey(x => x.Id);
        builder.ConfigureBaseEntity();
        builder.Property(x => x.FmcsaScoringRunId).HasColumnName("fmcsa_scoring_run_id");
        builder.Property(x => x.Basic).HasColumnName("basic").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Measure).HasColumnName("measure").HasPrecision(12, 4);
        builder.Property(x => x.Percentile).HasColumnName("percentile").HasPrecision(6, 2);
        builder.Property(x => x.IsPrioritized).HasColumnName("is_prioritized");
        builder.Property(x => x.EventCount).HasColumnName("event_count");
        builder.Property(x => x.OutOfServiceCount).HasColumnName("out_of_service_count");
        builder.Property(x => x.TrendDirection).HasColumnName("trend_direction").HasMaxLength(20);
        builder.HasIndex(x => new { x.FmcsaScoringRunId, x.Basic }).IsUnique();
        builder.HasOne(x => x.ScoringRun).WithMany(x => x.BasicScores)
            .HasForeignKey(x => x.FmcsaScoringRunId).OnDelete(DeleteBehavior.Cascade);
    }
}
