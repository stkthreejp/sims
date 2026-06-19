using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class XeroOAuthTokenConfiguration : IEntityTypeConfiguration<XeroOAuthToken>
{
    public void Configure(EntityTypeBuilder<XeroOAuthToken> b)
    {
        b.ToTable("xero_oauth_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.XeroTenantId).IsRequired().HasMaxLength(100);
        b.Property(x => x.AccessToken).IsRequired().HasMaxLength(4000);
        b.HasIndex(x => x.TenantId).IsUnique()
            .HasDatabaseName("ix_xero_oauth_tokens_tenant");
    }
}
