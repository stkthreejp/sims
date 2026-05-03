using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class SubmissionEquipmentConfiguration : IEntityTypeConfiguration<SubmissionEquipment>
{
    public void Configure(EntityTypeBuilder<SubmissionEquipment> builder)
    {
        builder.ToTable("submission_equipment");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Make).HasMaxLength(100);
        builder.Property(e => e.Model).HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.SerialNumber).HasMaxLength(100);
        builder.Property(e => e.Value).HasPrecision(18, 2);
        builder.Property(e => e.TerritoryCode).HasMaxLength(20);
        builder.Property(e => e.Deductible).HasPrecision(18, 2);
        builder.Property(e => e.SettlementBasis).HasMaxLength(10);

        builder.HasOne(e => e.Submission).WithMany(s => s.Equipment)
            .HasForeignKey(e => e.SubmissionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.EquipmentType).WithMany()
            .HasForeignKey(e => e.EquipmentTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
