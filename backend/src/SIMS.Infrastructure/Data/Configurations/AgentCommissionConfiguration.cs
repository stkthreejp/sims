using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class AgentCommissionConfiguration : IEntityTypeConfiguration<AgentCommission>
{
    public void Configure(EntityTypeBuilder<AgentCommission> builder)
    {
        builder.ToTable("agent_commissions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CommissionRate).HasColumnType("numeric(8,6)");
        builder.Property(e => e.LineOfBusiness).HasMaxLength(50);

        builder.HasOne(e => e.Agent)
            .WithMany()
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.AgentId, e.LineOfBusiness, e.EffectiveDate }).IsUnique();
        builder.HasIndex(e => new { e.AgentId, e.DisabledDate });
    }
}
