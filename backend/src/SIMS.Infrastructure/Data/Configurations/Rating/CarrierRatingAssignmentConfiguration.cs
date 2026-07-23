using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class CarrierRatingAssignmentConfiguration : IEntityTypeConfiguration<CarrierRatingAssignment>
{
    public void Configure(EntityTypeBuilder<CarrierRatingAssignment> builder)
    {
        const string programScopeCanonicalCheck =
            """
            (
                program_configuration_id IS NULL
                AND program_carrier_line_of_business_id IS NULL
            )
            OR (
                program_configuration_id IS NOT NULL
                AND program_carrier_line_of_business_id IS NOT NULL
            )
            """;

        builder.ToTable("carrier_rating_assignments", t => t.HasCheckConstraint("ck_carrier_rating_assignment_program_scope_canonical", programScopeCanonicalCheck));
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted");
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");

        builder.Property(a => a.ProgramConfigurationId).HasColumnName("program_configuration_id");
        builder.Property(a => a.CarrierId).HasColumnName("carrier_id");
        builder.Property(a => a.LineOfBusiness).HasColumnName("line_of_business");
        builder.Property(a => a.ProgramCarrierLineOfBusinessId).HasColumnName("program_carrier_line_of_business_id");
        builder.Property(a => a.RatingPlanVersionId).HasColumnName("rating_plan_version_id");

        // Partial unique indexes must exclude soft-deleted rows, else create->delete->recreate
        // of the same carrier/LOB collides on a tombstoned row and 500s (WS5-R Batch 2, A2.1).
        builder.HasIndex(a => new { a.CarrierId, a.LineOfBusiness })
            .IsUnique()
            .HasFilter("program_configuration_id IS NULL AND is_deleted = false");
        builder.HasIndex(a => new { a.ProgramConfigurationId, a.CarrierId, a.LineOfBusiness })
            .IsUnique()
            .HasFilter("program_configuration_id IS NOT NULL AND is_deleted = false");
        builder.HasIndex(a => a.ProgramCarrierLineOfBusinessId)
            .HasDatabaseName("ix_carrier_rating_assignment_program_lob_scope");

        builder.HasOne(a => a.ProgramConfiguration).WithMany()
            .HasForeignKey(a => a.ProgramConfigurationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ProgramCarrierLineOfBusiness).WithMany()
            .HasForeignKey(a => a.ProgramCarrierLineOfBusinessId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Carrier).WithMany()
            .HasForeignKey(a => a.CarrierId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.RatingPlanVersion).WithMany()
            .HasForeignKey(a => a.RatingPlanVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
