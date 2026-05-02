using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class CarrierLineOfBusinessConfiguration : IEntityTypeConfiguration<CarrierLineOfBusiness>
{
    public void Configure(EntityTypeBuilder<CarrierLineOfBusiness> builder)
    {
        builder.ToTable("carrier_lines_of_business");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.CarrierId, x.LineOfBusiness }).IsUnique();

        builder.HasOne(x => x.Carrier)
            .WithMany(c => c.LinesOfBusiness)
            .HasForeignKey(x => x.CarrierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
