using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class CarrierConfiguration : IEntityTypeConfiguration<Carrier>
{
    public void Configure(EntityTypeBuilder<Carrier> builder)
    {
        builder.ToTable("carriers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(c => c.Name).IsUnique();
        builder.Property(c => c.Naic).HasMaxLength(20);
        builder.Property(c => c.AmBestRating).HasMaxLength(10);

        builder.HasMany(c => c.Contacts)
            .WithOne(cc => cc.Carrier)
            .HasForeignKey(cc => cc.CarrierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
