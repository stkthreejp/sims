using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ProgramCarrierLobStateConfiguration : IEntityTypeConfiguration<ProgramCarrierLobState>
{
    public void Configure(EntityTypeBuilder<ProgramCarrierLobState> builder)
    {
        builder.ToTable("program_carrier_lob_states");

        builder.Property(x => x.StateCode).IsRequired().HasMaxLength(2);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => new { x.ProgramCarrierLineOfBusinessId, x.StateCode }).IsUnique();
        builder.HasIndex(x => x.IsActive);

        builder.HasOne(x => x.ProgramCarrierLineOfBusiness)
            .WithMany(l => l.States)
            .HasForeignKey(x => x.ProgramCarrierLineOfBusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
