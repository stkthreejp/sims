using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class QuoteUWWriteupConfiguration : IEntityTypeConfiguration<QuoteUWWriteup>
{
    public void Configure(EntityTypeBuilder<QuoteUWWriteup> builder)
    {
        builder.ToTable("quote_uw_writeups");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.IsDeleted).HasColumnName("is_deleted");
        builder.Property(w => w.DeletedAt).HasColumnName("deleted_at");

        builder.Property(w => w.QuoteId).HasColumnName("quote_id");
        builder.Property(w => w.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
        builder.Property(w => w.Decision).HasColumnName("decision").HasConversion<string>().HasMaxLength(50);
        builder.Property(w => w.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
        builder.Property(w => w.SchemaVersion).HasColumnName("schema_version");
        builder.Property(w => w.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(w => w.SubmittedById).HasColumnName("submitted_by_id");
        builder.Property(w => w.ApprovedAt).HasColumnName("approved_at");
        builder.Property(w => w.ApprovedById).HasColumnName("approved_by_id");

        builder.HasIndex(w => w.QuoteId).IsUnique();

        builder.HasOne(w => w.Quote).WithOne(q => q.UWWriteup)
            .HasForeignKey<QuoteUWWriteup>(w => w.QuoteId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.SubmittedBy).WithMany()
            .HasForeignKey(w => w.SubmittedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.ApprovedBy).WithMany()
            .HasForeignKey(w => w.ApprovedById).OnDelete(DeleteBehavior.Restrict);
    }
}
