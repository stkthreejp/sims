using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class LegalRequirementChangeLogConfiguration : IEntityTypeConfiguration<LegalRequirementChangeLog>
{
    public void Configure(EntityTypeBuilder<LegalRequirementChangeLog> builder)
    {
        builder.ToTable("legal_requirement_change_logs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ChangeType).IsRequired().HasMaxLength(40);
        builder.Property(l => l.FieldName).IsRequired().HasMaxLength(120);
        builder.Property(l => l.Comment).HasMaxLength(2000);
        builder.Property(l => l.ChangedByName).IsRequired().HasMaxLength(200);

        builder.HasOne(l => l.RequirementSection).WithMany()
            .HasForeignKey(l => l.RequirementSectionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.ScanResult).WithMany()
            .HasForeignKey(l => l.ScanResultId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.ChangedBy).WithMany()
            .HasForeignKey(l => l.ChangedById).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(l => new { l.RequirementSectionId, l.ChangedAt });
        builder.HasIndex(l => l.ScanResultId);
    }
}
