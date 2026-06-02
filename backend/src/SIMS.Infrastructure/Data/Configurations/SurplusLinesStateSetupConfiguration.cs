using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class SurplusLinesStateSetupConfiguration : IEntityTypeConfiguration<SurplusLinesStateSetup>
{
    public void Configure(EntityTypeBuilder<SurplusLinesStateSetup> builder)
    {
        const string programScopeCanonicalCheck =
            """
            (
                "ProgramConfigurationId" IS NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NOT NULL
                AND "LineOfBusiness" IS NOT NULL
                AND "ProgramCarrierLobStateId" IS NOT NULL
            )
            """;

        builder.ToTable("surplus_lines_state_setups", t => t.HasCheckConstraint("ck_surplus_lines_state_setup_program_scope_canonical", programScopeCanonicalCheck));
        builder.HasKey(s => s.Id);

        builder.Property(s => s.StateCode).IsRequired().HasMaxLength(2);
        builder.Property(s => s.LicenseHolderType).IsRequired().HasMaxLength(30);
        builder.Property(s => s.FilingBrokerName).IsRequired().HasMaxLength(200);
        builder.Property(s => s.LicenseNumber).IsRequired().HasMaxLength(100);
        builder.Property(s => s.LicenseState).IsRequired().HasMaxLength(2);
        builder.Property(s => s.BrokerAddressLine1).HasMaxLength(200);
        builder.Property(s => s.BrokerAddressLine2).HasMaxLength(200);
        builder.Property(s => s.BrokerCity).HasMaxLength(100);
        builder.Property(s => s.BrokerState).HasMaxLength(2);
        builder.Property(s => s.BrokerZipCode).HasMaxLength(20);
        builder.Property(s => s.BrokerCountry).HasMaxLength(3);
        builder.Property(s => s.StampingWording).HasMaxLength(2000);
        builder.Property(s => s.RequiredNoticeText).HasMaxLength(2000);
        builder.Property(s => s.PaperworkNotes).HasMaxLength(2000);
        builder.Property(s => s.FilingNotes).HasMaxLength(2000);
        builder.Property(s => s.FilingFrequency).HasMaxLength(30);
        builder.Property(s => s.FilingMethod).HasMaxLength(50);
        builder.Property(s => s.FilingPortalUrl).HasMaxLength(500);
        builder.Property(s => s.RequiredFilingFormsJson)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");
        builder.Property(s => s.DiligentSearchNotes).HasMaxLength(2000);
        builder.Property(s => s.AffidavitNotes).HasMaxLength(2000);

        builder.HasIndex(s => new
        {
            s.StateCode,
            s.ProgramConfigurationId,
            s.CarrierId,
            s.LineOfBusiness,
            s.EffectiveDate,
        }).HasDatabaseName("ix_surplus_lines_state_setup_lookup");
        builder.HasIndex(s => s.ProgramCarrierLobStateId)
            .HasDatabaseName("ix_surplus_lines_state_setup_program_state_scope");

        builder.HasOne(s => s.ProgramConfiguration)
            .WithMany()
            .HasForeignKey(s => s.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Carrier)
            .WithMany()
            .HasForeignKey(s => s.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ProgramCarrierLobState)
            .WithMany()
            .HasForeignKey(s => s.ProgramCarrierLobStateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SurplusLinesTaxFeeDefinition)
            .WithMany()
            .HasForeignKey(s => s.SurplusLinesTaxFeeDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.StampingFeeDefinition)
            .WithMany()
            .HasForeignKey(s => s.StampingFeeDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.FilingFeeDefinition)
            .WithMany()
            .HasForeignKey(s => s.FilingFeeDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.StatePayee)
            .WithMany()
            .HasForeignKey(s => s.StatePayeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.FilingPayee)
            .WithMany()
            .HasForeignKey(s => s.FilingPayeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
