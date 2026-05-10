using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Infrastructure.Data.Configurations.Fmcsa;
using SIMS.Domain.Entities.FmcsaAnalytics;

namespace SIMS.Infrastructure.Data.Configurations.FmcsaAnalytics;

public class FmcsaBasicPeerMeasureConfiguration : IEntityTypeConfiguration<FmcsaBasicPeerMeasure>
{
    public void Configure(EntityTypeBuilder<FmcsaBasicPeerMeasure> builder)
    {
        builder.ToTable("fmcsa_basic_peer_measures");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.SnapshotMonth).HasColumnName("snapshot_month").HasMaxLength(7).IsRequired();
        builder.Property(x => x.UsDotNumber).HasColumnName("us_dot_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Basic).HasColumnName("basic").HasMaxLength(80).IsRequired();
        builder.Property(x => x.OfficialMeasure).HasColumnName("official_measure").HasPrecision(12, 4);
        builder.Property(x => x.SimsMeasure).HasColumnName("sims_measure").HasPrecision(12, 4);
        builder.Property(x => x.InspectionWithViolationCount).HasColumnName("inspection_with_violation_count");
        builder.Property(x => x.ViolationCount).HasColumnName("violation_count");
        builder.Property(x => x.OutOfServiceCount).HasColumnName("out_of_service_count");
        builder.Property(x => x.WeightedViolationScore).HasColumnName("weighted_violation_score").HasPrecision(14, 4);
        builder.Property(x => x.Exposure).HasColumnName("exposure").HasPrecision(14, 4);
        builder.Property(x => x.PeerGroupKey).HasColumnName("peer_group_key").HasMaxLength(80).IsRequired();
        builder.Property(x => x.PeerRank).HasColumnName("peer_rank");
        builder.Property(x => x.PeerPopulation).HasColumnName("peer_population");
        builder.Property(x => x.SimsPercentile).HasColumnName("sims_percentile").HasPrecision(6, 2);
        builder.HasIndex(x => new { x.SnapshotMonth, x.UsDotNumber, x.Basic }).IsUnique();
        builder.HasIndex(x => new { x.SnapshotMonth, x.Basic, x.PeerGroupKey, x.SimsMeasure });
    }
}
