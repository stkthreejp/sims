using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Bordereaux;

namespace SIMS.Infrastructure.Data.Configurations.Bordereaux;

public class BordereauxProfileConfiguration : IEntityTypeConfiguration<BordereauxProfile>
{
    public void Configure(EntityTypeBuilder<BordereauxProfile> builder)
    {
        builder.ToTable("bordereaux_profiles");

        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.StateCode).HasMaxLength(2);
        builder.Property(x => x.RequiredTabsJson).HasColumnType("jsonb");
        builder.Property(x => x.RequiredColumnsJson).HasColumnType("jsonb");
        builder.Property(x => x.MappingRulesJson).HasColumnType("jsonb");
        builder.Property(x => x.StaticValuesJson).HasColumnType("jsonb");
        builder.Property(x => x.ValidationRulesJson).HasColumnType("jsonb");
        builder.Property(x => x.IncludedTransactionTypesJson).HasColumnType("jsonb");
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => new
        {
            x.ProgramConfigurationId,
            x.CarrierId,
            x.ReportType,
            x.LineOfBusiness,
            x.StateCode,
            x.IsActive,
        }).IsUnique();
        builder.HasIndex(x => x.IsActive);

        builder.HasOne(x => x.ProgramConfiguration)
            .WithMany()
            .HasForeignKey(x => x.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Carrier)
            .WithMany()
            .HasForeignKey(x => x.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
