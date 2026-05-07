using SIMS.Domain.Entities.Fmcsa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations.Fmcsa;

public class FmcsaCarrierSnapshotConfiguration : IEntityTypeConfiguration<FmcsaCarrierSnapshot>
{
    public void Configure(EntityTypeBuilder<FmcsaCarrierSnapshot> builder)
    {
        builder.ToTable("fmcsa_carrier_snapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UsDotNumber).HasColumnName("us_dot_number").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SnapshotMonth).HasColumnName("snapshot_month").HasMaxLength(7).IsRequired();
        builder.Property(x => x.LegalName).HasColumnName("legal_name").HasMaxLength(250).IsRequired();
        builder.Property(x => x.DbaName).HasColumnName("dba_name").HasMaxLength(250);
        builder.Property(x => x.PhysicalAddress).HasColumnName("physical_address").HasMaxLength(300);
        builder.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(2);
        builder.Property(x => x.ZipCode).HasColumnName("zip_code").HasMaxLength(10);
        builder.Property(x => x.PowerUnits).HasColumnName("power_units");
        builder.Property(x => x.DriverCount).HasColumnName("driver_count");
        builder.Property(x => x.Mileage).HasColumnName("mileage");
        builder.Property(x => x.MileageYear).HasColumnName("mileage_year");
        builder.Property(x => x.OperationClassification).HasColumnName("operation_classification").HasMaxLength(100);
        builder.Property(x => x.CarrierOperation).HasColumnName("carrier_operation").HasMaxLength(100);
        builder.Property(x => x.ImportedAt).HasColumnName("imported_at");
        builder.HasIndex(x => new { x.UsDotNumber, x.SnapshotMonth }).IsUnique();
    }
}
