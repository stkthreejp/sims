using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class FeeStateTaxabilityConfiguration : IEntityTypeConfiguration<FeeStateTaxability>
{
    public void Configure(EntityTypeBuilder<FeeStateTaxability> b)
    {
        b.ToTable("fee_state_taxability");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.StateCode).IsRequired().HasMaxLength(2);

        b.HasOne(x => x.FeeDefinition)
            .WithMany(x => x.StateTaxabilities)
            .HasForeignKey(x => x.FeeDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.FeeDefinitionId, x.StateCode }).IsUnique();
    }
}
