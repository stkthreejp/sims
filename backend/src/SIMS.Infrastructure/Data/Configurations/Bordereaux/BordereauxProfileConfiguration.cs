using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Bordereaux;

namespace SIMS.Infrastructure.Data.Configurations.Bordereaux;

public class BordereauxProfileConfiguration : IEntityTypeConfiguration<BordereauxProfile>
{
    public void Configure(EntityTypeBuilder<BordereauxProfile> builder)
    {
        const string programScopeCanonicalCheck =
            """
            (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NOT NULL
                AND "LineOfBusiness" IS NULL
                AND "StateCode" IS NULL
                AND "ProgramCarrierId" IS NOT NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NOT NULL
                AND "LineOfBusiness" IS NOT NULL
                AND "StateCode" IS NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NOT NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NOT NULL
                AND "LineOfBusiness" IS NOT NULL
                AND "StateCode" IS NOT NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NOT NULL
            )
            """;

        builder.ToTable("bordereaux_profiles", t => t.HasCheckConstraint("ck_bordereaux_profile_program_scope_canonical", programScopeCanonicalCheck));

        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.StateCode).HasMaxLength(2);
        builder.Property(x => x.RequiredTabsJson).HasColumnType("jsonb");
        builder.Property(x => x.RequiredColumnsJson).HasColumnType("jsonb");
        builder.Property(x => x.MappingRulesJson).HasColumnType("jsonb");
        builder.Property(x => x.StaticValuesJson).HasColumnType("jsonb");
        builder.Property(x => x.ValidationRulesJson).HasColumnType("jsonb");
        builder.Property(x => x.IncludedTransactionTypesJson).HasColumnType("jsonb");
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => new
        {
            x.ProgramConfigurationId,
            x.CarrierId,
            x.ReportType,
            x.LineOfBusiness,
            x.StateCode,
            x.IsActive,
        })
            .IsUnique()
            .AreNullsDistinct(false);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.ProgramCarrierId)
            .HasDatabaseName("ix_bordereaux_profiles_program_carrier_scope");
        builder.HasIndex(x => x.ProgramCarrierLineOfBusinessId)
            .HasDatabaseName("ix_bordereaux_profiles_program_lob_scope");
        builder.HasIndex(x => x.ProgramCarrierLobStateId)
            .HasDatabaseName("ix_bordereaux_profiles_program_state_scope");

        builder.HasOne(x => x.ProgramConfiguration)
            .WithMany()
            .HasForeignKey(x => x.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Carrier)
            .WithMany()
            .HasForeignKey(x => x.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProgramCarrier)
            .WithMany()
            .HasForeignKey(x => x.ProgramCarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProgramCarrierLineOfBusiness)
            .WithMany()
            .HasForeignKey(x => x.ProgramCarrierLineOfBusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProgramCarrierLobState)
            .WithMany()
            .HasForeignKey(x => x.ProgramCarrierLobStateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
