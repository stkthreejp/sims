using SIMS.Domain.Entities.Fmcsa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations.Fmcsa;

public class FmcsaScoringRunConfiguration : IEntityTypeConfiguration<FmcsaScoringRun>
{
    public void Configure(EntityTypeBuilder<FmcsaScoringRun> builder)
    {
        builder.ToTable("fmcsa_scoring_runs");
        builder.HasKey(x => x.Id);
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UsDotNumber).HasColumnName("us_dot_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SnapshotMonth).HasColumnName("snapshot_month").HasMaxLength(7).IsRequired();
        builder.Property(x => x.MethodologyVersion).HasColumnName("methodology_version").HasMaxLength(50).IsRequired();
        builder.Property(x => x.GeneratedAt).HasColumnName("generated_at");
        builder.HasIndex(x => new { x.UsDotNumber, x.SnapshotMonth });
    }
}
