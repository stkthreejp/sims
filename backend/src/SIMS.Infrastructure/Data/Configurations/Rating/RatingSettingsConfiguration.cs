using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class RatingSettingsConfiguration : IEntityTypeConfiguration<RatingSettings>
{
    public void Configure(EntityTypeBuilder<RatingSettings> builder)
    {
        builder.ToTable("rating_settings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted");
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.ShadowModeGL).HasColumnName("shadow_mode_gl");
        builder.Property(s => s.ShadowModeIM).HasColumnName("shadow_mode_im");
        builder.Property(s => s.ShadowModeAL).HasColumnName("shadow_mode_al");
        builder.Property(s => s.ShadowModeAPD).HasColumnName("shadow_mode_apd");
    }
}
