using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyPackageConfigurationConfiguration : IEntityTypeConfiguration<PolicyPackageConfiguration>
{
    public void Configure(EntityTypeBuilder<PolicyPackageConfiguration> builder)
    {
        builder.ToTable("policy_package_configurations");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.State).HasMaxLength(2).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(250).IsRequired();

        builder.HasIndex(p => new { p.ProgramConfigurationId, p.CarrierId, p.LineOfBusiness, p.State, p.IsDeleted })
            .HasDatabaseName("ix_policy_package_program_lookup");

        builder.HasOne(p => p.Carrier)
            .WithMany()
            .HasForeignKey(p => p.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ProgramConfiguration)
            .WithMany()
            .HasForeignKey(p => p.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
