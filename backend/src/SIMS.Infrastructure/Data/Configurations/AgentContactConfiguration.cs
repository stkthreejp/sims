using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class AgentContactConfiguration : IEntityTypeConfiguration<AgentContact>
{
    public void Configure(EntityTypeBuilder<AgentContact> builder)
    {
        builder.ToTable("agent_contacts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.LastName).HasMaxLength(100);
        builder.Property(c => c.Title).HasMaxLength(100);
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.Phone).HasMaxLength(30);
    }
}
