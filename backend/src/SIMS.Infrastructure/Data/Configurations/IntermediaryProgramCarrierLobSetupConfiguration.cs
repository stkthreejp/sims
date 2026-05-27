using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class IntermediaryProgramCarrierLobSetupConfiguration : IEntityTypeConfiguration<IntermediaryProgramCarrierLobSetup>
{
    public void Configure(EntityTypeBuilder<IntermediaryProgramCarrierLobSetup> builder)
    {
        builder.ToTable("intermediary_program_carrier_lob_setups");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.BrokerageRate).HasPrecision(9, 6);
        builder.Property(s => s.Notes).HasMaxLength(1000);

        builder.HasIndex(s => new
        {
            s.ProgramConfigurationId,
            s.CarrierId,
            s.LineOfBusiness,
            s.EffectiveDate,
        }).HasDatabaseName("ix_intermediary_setup_lookup");

        builder.HasOne(s => s.Intermediary)
            .WithMany(i => i.ProgramCarrierLobSetups)
            .HasForeignKey(s => s.IntermediaryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ProgramConfiguration)
            .WithMany()
            .HasForeignKey(s => s.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Carrier)
            .WithMany()
            .HasForeignKey(s => s.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.PayablePayee)
            .WithMany()
            .HasForeignKey(s => s.PayablePayeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
