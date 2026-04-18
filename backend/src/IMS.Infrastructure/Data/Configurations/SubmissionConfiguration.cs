using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Data.Configurations;

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submissions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SubmissionNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(s => s.SubmissionNumber).IsUnique();

        builder.HasOne(s => s.Insured).WithMany(i => i.Submissions)
            .HasForeignKey(s => s.InsuredId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Agent).WithMany(a => a.Submissions)
            .HasForeignKey(s => s.AgentId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.Underwriter).WithMany()
            .HasForeignKey(s => s.UnderwriterId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.AssistantUW).WithMany()
            .HasForeignKey(s => s.AssistantUWId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.CreatedBy).WithMany()
            .HasForeignKey(s => s.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
