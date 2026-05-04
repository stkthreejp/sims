using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class InsuredConfiguration : IEntityTypeConfiguration<Insured>
{
    public void Configure(EntityTypeBuilder<Insured> builder)
    {
        builder.ToTable("insureds");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.FirstName).HasMaxLength(100);
        builder.Property(i => i.LastName).HasMaxLength(100);
        builder.Property(i => i.CompanyName).HasMaxLength(200);
        builder.Property(i => i.Dba).HasMaxLength(200);
        builder.Property(i => i.TaxId).HasMaxLength(20);
        builder.Property(i => i.Email).HasMaxLength(200);
        builder.Property(i => i.Phone).HasMaxLength(30);
        builder.Property(i => i.PhoneAlt).HasMaxLength(30);
        builder.Property(i => i.AddressLine1).IsRequired().HasMaxLength(200);
        builder.Property(i => i.AddressLine2).HasMaxLength(200);
        builder.Property(i => i.City).IsRequired().HasMaxLength(100);
        builder.Property(i => i.State).IsRequired().HasMaxLength(2);
        builder.Property(i => i.ZipCode).IsRequired().HasMaxLength(10);
        builder.Property(i => i.County).HasMaxLength(100);
        builder.Property(i => i.OperationType).HasColumnName("operation_type").HasMaxLength(200);
        builder.Property(i => i.CreditScore).HasColumnName("credit_score");
        builder.Property(i => i.Website).HasColumnName("website").HasMaxLength(500);

        builder.Ignore(i => i.DisplayName);

        builder.HasOne(i => i.CreatedBy).WithMany()
            .HasForeignKey(i => i.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
