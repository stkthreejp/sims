using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyPackageFormConfiguration : IEntityTypeConfiguration<PolicyPackageForm>
{
    public void Configure(EntityTypeBuilder<PolicyPackageForm> builder)
    {
        builder.ToTable("policy_package_forms");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.TriggerConditionJson).HasColumnType("jsonb");
        builder.Property(f => f.Notes).HasMaxLength(1000);

        builder.HasIndex(f => new { f.PolicyPackageConfigurationId, f.SequenceOrder, f.IsDeleted });

        builder.HasOne(f => f.PolicyPackageConfiguration)
            .WithMany(p => p.Forms)
            .HasForeignKey(f => f.PolicyPackageConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.PolicyFormTemplate)
            .WithMany()
            .HasForeignKey(f => f.PolicyFormTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
