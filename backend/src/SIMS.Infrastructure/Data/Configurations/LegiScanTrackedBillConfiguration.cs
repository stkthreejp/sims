using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class LegiScanTrackedBillConfiguration : IEntityTypeConfiguration<LegiScanTrackedBill>
{
    public void Configure(EntityTypeBuilder<LegiScanTrackedBill> builder)
    {
        builder.ToTable("legiscan_tracked_bills");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.State).IsRequired().HasMaxLength(2);
        builder.Property(b => b.BillNumber).IsRequired().HasMaxLength(40);
        builder.Property(b => b.Title).IsRequired().HasMaxLength(500);
        builder.Property(b => b.Description).HasMaxLength(4000);
        builder.Property(b => b.ChangeHash).HasMaxLength(64);
        builder.Property(b => b.Url).HasMaxLength(1000);
        builder.Property(b => b.Stance).HasMaxLength(20);
        builder.Property(b => b.RawBillJson).HasColumnType("jsonb");

        builder.HasIndex(b => b.BillId).IsUnique();
        builder.HasIndex(b => b.IsActive);
        builder.HasIndex(b => new { b.State, b.BillNumber });
        builder.HasIndex(b => b.ChangeHash);
    }
}
