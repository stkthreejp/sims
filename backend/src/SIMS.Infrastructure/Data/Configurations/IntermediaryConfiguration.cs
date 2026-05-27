using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class IntermediaryConfiguration : IEntityTypeConfiguration<Intermediary>
{
    public void Configure(EntityTypeBuilder<Intermediary> builder)
    {
        builder.ToTable("intermediaries");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
        builder.Property(i => i.ReferenceNumber).HasMaxLength(80);
        builder.Property(i => i.Email).HasMaxLength(200);
        builder.Property(i => i.Phone).HasMaxLength(30);
        builder.Property(i => i.AddressLine1).HasMaxLength(200);
        builder.Property(i => i.AddressLine2).HasMaxLength(200);
        builder.Property(i => i.City).HasMaxLength(100);
        builder.Property(i => i.State).HasMaxLength(40);
        builder.Property(i => i.ZipCode).HasMaxLength(20);
        builder.Property(i => i.Country).HasMaxLength(3);
        builder.Property(i => i.BankName).HasMaxLength(200);
        builder.Property(i => i.BankAccountName).HasMaxLength(200);
        builder.Property(i => i.BankAccountLast4).HasMaxLength(4);
        builder.Property(i => i.BankRoutingNumber).HasMaxLength(30);
        builder.Property(i => i.BankSwiftCode).HasMaxLength(30);
        builder.Property(i => i.BankInstructions).HasMaxLength(1000);
        builder.Property(i => i.Notes).HasMaxLength(1000);

        builder.HasIndex(i => i.Name);
        builder.HasIndex(i => i.IsActive);
    }
}
