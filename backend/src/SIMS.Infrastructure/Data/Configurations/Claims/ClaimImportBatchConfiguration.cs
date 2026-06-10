using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations.Claims;

public class ClaimImportBatchConfiguration : IEntityTypeConfiguration<ClaimImportBatch>
{
    public void Configure(EntityTypeBuilder<ClaimImportBatch> builder)
    {
        builder.ToTable("claim_import_batches");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.FileName).HasMaxLength(500);
        builder.Property(b => b.CarrierName).HasMaxLength(500);
        builder.Property(b => b.TpaName).HasMaxLength(500);
        builder.Property(b => b.Status).HasMaxLength(50);

        builder.HasIndex(b => b.ImportedById);
        builder.HasIndex(b => b.ValuationDate);
        builder.HasIndex(b => b.CreatedAt);

        builder.HasOne(b => b.ImportedBy)
            .WithMany()
            .HasForeignKey(b => b.ImportedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
