using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("agents");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.AgencyName).HasMaxLength(200);
        builder.Property(a => a.LicenseNumber).HasMaxLength(50);
        builder.Property(a => a.Email).HasMaxLength(200);
        builder.Property(a => a.Phone).HasMaxLength(30);

        builder.HasMany(a => a.Locations)
            .WithOne(l => l.Agent)
            .HasForeignKey(l => l.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
