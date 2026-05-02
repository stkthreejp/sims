using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class FeeDefinitionConfiguration : IEntityTypeConfiguration<FeeDefinition>
{
    public void Configure(EntityTypeBuilder<FeeDefinition> b)
    {
        b.ToTable("fee_definitions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Code).IsRequired().HasMaxLength(100);
        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        b.Property(x => x.FeeCategory).IsRequired().HasMaxLength(50);

        b.HasOne(x => x.LedgerAccount)
            .WithMany()
            .HasForeignKey(x => x.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}
