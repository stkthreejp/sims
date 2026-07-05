using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class CompanyLicenseConfiguration : IEntityTypeConfiguration<CompanyLicense>
{
    public void Configure(EntityTypeBuilder<CompanyLicense> b)
    {
        b.ToTable("company_licenses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.HolderName).HasColumnName("holder_name").HasMaxLength(200);
        b.Property(x => x.LicenseNumber).HasColumnName("license_number").HasMaxLength(100);
        b.Property(x => x.LicenseState).HasColumnName("license_state").HasMaxLength(2);
        b.Property(x => x.LicenseType).HasColumnName("license_type").HasMaxLength(100);
        b.Property(x => x.EffectiveDate).HasColumnName("effective_date");
        b.Property(x => x.ExpirationDate).HasColumnName("expiration_date");
        b.Property(x => x.AddressLine1).HasColumnName("address_line1").HasMaxLength(200);
        b.Property(x => x.AddressLine2).HasColumnName("address_line2").HasMaxLength(200);
        b.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
        b.Property(x => x.State).HasColumnName("state").HasMaxLength(2);
        b.Property(x => x.ZipCode).HasColumnName("zip_code").HasMaxLength(20);
        b.Property(x => x.Country).HasColumnName("country").HasMaxLength(60);
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        b.HasIndex(x => x.IsActive);
    }
}
