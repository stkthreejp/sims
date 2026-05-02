using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class PayeeConfiguration : IEntityTypeConfiguration<Payee>
{
    public void Configure(EntityTypeBuilder<Payee> b)
    {
        b.ToTable("payees");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.PayeeType).IsRequired().HasMaxLength(30);
        b.Property(x => x.ExternalReference).HasMaxLength(100);
    }
}
