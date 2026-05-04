using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class QuoteUWWriteupConditionConfiguration : IEntityTypeConfiguration<QuoteUWWriteupCondition>
{
    public void Configure(EntityTypeBuilder<QuoteUWWriteupCondition> builder)
    {
        builder.ToTable("quote_uw_writeup_conditions");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted");
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");

        builder.Property(c => c.WriteupId).HasColumnName("writeup_id");
        builder.Property(c => c.Text).HasColumnName("text").HasMaxLength(1000);
        builder.Property(c => c.Required).HasColumnName("required");
        builder.Property(c => c.Satisfied).HasColumnName("satisfied");
        builder.Property(c => c.SortOrder).HasColumnName("sort_order");

        builder.HasIndex(c => c.WriteupId);

        builder.HasOne(c => c.Writeup).WithMany(w => w.Conditions)
            .HasForeignKey(c => c.WriteupId).OnDelete(DeleteBehavior.Cascade);
    }
}
