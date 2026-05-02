using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Data.Configurations;

public class SystemEventConfiguration : IEntityTypeConfiguration<SystemEvent>
{
    public void Configure(EntityTypeBuilder<SystemEvent> builder)
    {
        builder.ToTable("system_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.HasIndex(e => e.EventName).IsUnique();
    }
}
