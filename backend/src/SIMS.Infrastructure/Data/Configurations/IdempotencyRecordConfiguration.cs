using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Key).IsRequired().HasMaxLength(200);
        builder.Property(r => r.RequestPath).IsRequired().HasMaxLength(500);
        builder.Property(r => r.ResponseBody).IsRequired();
        builder.HasIndex(r => new { r.Key, r.RequestPath }).IsUnique();
        builder.HasIndex(r => r.ExpiresAt);
    }
}
