using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class QboOAuthTokenConfiguration : IEntityTypeConfiguration<QboOAuthToken>
{
    public void Configure(EntityTypeBuilder<QboOAuthToken> b)
    {
        b.ToTable("qbo_oauth_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.RealmId).IsRequired().HasMaxLength(50);
        b.Property(x => x.AccessToken).IsRequired().HasMaxLength(4000);
        b.Property(x => x.RefreshToken).IsRequired().HasMaxLength(500);
        b.HasIndex(x => new { x.TenantId, x.RealmId }).IsUnique()
            .HasDatabaseName("ix_qbo_oauth_tokens_tenant_realm");
    }
}
