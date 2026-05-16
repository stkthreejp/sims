using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyNumberAssignmentConfiguration : IEntityTypeConfiguration<PolicyNumberAssignment>
{
    public void Configure(EntityTypeBuilder<PolicyNumberAssignment> builder)
    {
        builder.ToTable("policy_number_assignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.State).HasMaxLength(2);
        builder.HasIndex(a => new { a.CarrierId, a.WritingCompanyId, a.LineOfBusiness, a.State, a.IsActive });

        builder.HasOne(a => a.PolicyNumberSequence)
            .WithMany(s => s.Assignments)
            .HasForeignKey(a => a.PolicyNumberSequenceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Carrier)
            .WithMany()
            .HasForeignKey(a => a.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
