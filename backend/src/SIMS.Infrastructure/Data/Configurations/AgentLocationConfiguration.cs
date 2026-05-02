using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class AgentLocationConfiguration : IEntityTypeConfiguration<AgentLocation>
{
    public void Configure(EntityTypeBuilder<AgentLocation> builder)
    {
        builder.ToTable("agent_locations");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).HasMaxLength(100);
        builder.Property(l => l.AddressLine1).HasMaxLength(200);
        builder.Property(l => l.AddressLine2).HasMaxLength(200);
        builder.Property(l => l.City).HasMaxLength(100);
        builder.Property(l => l.State).HasMaxLength(2);
        builder.Property(l => l.ZipCode).HasMaxLength(10);
        builder.Property(l => l.Phone).HasMaxLength(30);

        builder.HasMany(l => l.Contacts)
            .WithOne(c => c.Location)
            .HasForeignKey(c => c.AgentLocationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
