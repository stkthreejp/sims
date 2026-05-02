using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class GlAccountMapConfiguration : IEntityTypeConfiguration<GlAccountMap>
{
    public void Configure(EntityTypeBuilder<GlAccountMap> b)
    {
        b.ToTable("gl_account_maps");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.ExternalSystem).IsRequired().HasMaxLength(20);
        b.Property(x => x.ExternalId).IsRequired().HasMaxLength(100);

        b.HasOne(x => x.LedgerAccount)
            .WithMany()
            .HasForeignKey(x => x.LedgerAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.LedgerAccountId, x.ExternalSystem }).IsUnique();
    }
}
