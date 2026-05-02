using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Subject).HasMaxLength(200);
        builder.Property(n => n.Body).IsRequired().HasMaxLength(10000);

        builder.HasOne(n => n.Quote).WithMany(q => q.Notes)
            .HasForeignKey(n => n.QuoteId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.CreatedBy).WithMany()
            .HasForeignKey(n => n.CreatedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.UpdatedBy).WithMany()
            .HasForeignKey(n => n.UpdatedById).OnDelete(DeleteBehavior.SetNull);
    }
}
