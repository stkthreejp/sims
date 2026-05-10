using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Infrastructure.Data.Configurations.Fmcsa;
using SIMS.Domain.Entities.FmcsaAnalytics;

namespace SIMS.Infrastructure.Data.Configurations.FmcsaAnalytics;

public class FmcsaCarrierPeerSnapshotConfiguration : IEntityTypeConfiguration<FmcsaCarrierPeerSnapshot>
{
    public void Configure(EntityTypeBuilder<FmcsaCarrierPeerSnapshot> builder)
    {
        builder.ToTable("fmcsa_carrier_peer_snapshots");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.SnapshotMonth).HasColumnName("snapshot_month").HasMaxLength(7).IsRequired();
        builder.Property(x => x.UsDotNumber).HasColumnName("us_dot_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.LegalName).HasColumnName("legal_name").HasMaxLength(250);
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(2);
        builder.Property(x => x.PowerUnits).HasColumnName("power_units");
        builder.Property(x => x.DriverCount).HasColumnName("driver_count");
        builder.Property(x => x.Mileage).HasColumnName("mileage");
        builder.Property(x => x.MileageYear).HasColumnName("mileage_year");
        builder.Property(x => x.InspectionCount).HasColumnName("inspection_count");
        builder.Property(x => x.DriverInspectionCount).HasColumnName("driver_inspection_count");
        builder.Property(x => x.VehicleInspectionCount).HasColumnName("vehicle_inspection_count");
        builder.Property(x => x.DriverOosInspectionCount).HasColumnName("driver_oos_inspection_count");
        builder.Property(x => x.VehicleOosInspectionCount).HasColumnName("vehicle_oos_inspection_count");
        builder.HasIndex(x => new { x.SnapshotMonth, x.UsDotNumber }).IsUnique();
        builder.HasIndex(x => new { x.SnapshotMonth, x.PowerUnits });
    }
}
