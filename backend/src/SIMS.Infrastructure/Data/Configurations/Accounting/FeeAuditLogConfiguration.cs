using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class FeeAuditLogConfiguration : IEntityTypeConfiguration<FeeAuditLog>
{
    public void Configure(EntityTypeBuilder<FeeAuditLog> b)
    {
        b.ToTable("fee_audit_log");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.ChangeType).IsRequired().HasMaxLength(30);
        b.Property(x => x.Notes).HasMaxLength(2000);

        b.HasOne(x => x.FeeRuleVersion)
            .WithMany(x => x.AuditLogs)
            .HasForeignKey(x => x.FeeRuleVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
