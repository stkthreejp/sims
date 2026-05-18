using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class QuoteRatingSnapshotConfiguration : IEntityTypeConfiguration<QuoteRatingSnapshot>
{
    public void Configure(EntityTypeBuilder<QuoteRatingSnapshot> builder)
    {
        builder.ToTable("quote_rating_snapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted");
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");

        builder.Property(s => s.QuoteId).HasColumnName("quote_id");
        builder.Property(s => s.PolicyTransactionId).HasColumnName("policy_transaction_id");
        builder.Property(s => s.RatingPlanVersionId).HasColumnName("rating_plan_version_id");
        builder.Property(s => s.RatedAt).HasColumnName("rated_at");
        builder.Property(s => s.RatedById).HasColumnName("rated_by_id");
        builder.Property(s => s.ManualPremium).HasPrecision(18, 2).HasColumnName("manual_premium");
        builder.Property(s => s.ScheduleModifier).HasPrecision(6, 4).HasColumnName("schedule_modifier");
        builder.Property(s => s.ScheduleModifierReason).HasMaxLength(500).HasColumnName("schedule_modifier_reason");
        builder.Property(s => s.NewlyAcquiredEquipment).HasColumnName("newly_acquired_equipment");
        builder.Property(s => s.DebrisRemoval).HasColumnName("debris_removal");
        builder.Property(s => s.RentalReimbursement).HasColumnName("rental_reimbursement");
        builder.Property(s => s.TowingStorageRecovery).HasColumnName("towing_storage_recovery");
        builder.Property(s => s.Tria).HasColumnName("tria");
        builder.Property(s => s.EndorsementPremium).HasPrecision(18, 2).HasColumnName("endorsement_premium");
        builder.Property(s => s.GrandTotalPremium).HasPrecision(18, 2).HasColumnName("grand_total_premium");
        builder.Property(s => s.IsBoundSnapshot).HasColumnName("is_bound_snapshot");

        builder.HasIndex(s => s.QuoteId);
        builder.HasIndex(s => s.PolicyTransactionId);

        builder.HasOne(s => s.Quote).WithMany()
            .HasForeignKey(s => s.QuoteId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.PolicyTransaction).WithMany()
            .HasForeignKey(s => s.PolicyTransactionId).OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(s => s.RatingPlanVersion).WithMany()
            .HasForeignKey(s => s.RatingPlanVersionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.RatedBy).WithMany()
            .HasForeignKey(s => s.RatedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Lines).WithOne(l => l.Snapshot)
            .HasForeignKey(l => l.QuoteRatingSnapshotId).OnDelete(DeleteBehavior.Cascade);
    }
}
