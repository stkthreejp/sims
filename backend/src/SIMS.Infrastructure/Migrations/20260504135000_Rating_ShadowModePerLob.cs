using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    public partial class Rating_ShadowModePerLob : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE rating_settings
                    ADD COLUMN IF NOT EXISTS shadow_mode_gl boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS shadow_mode_im boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS shadow_mode_al boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS shadow_mode_apd boolean NOT NULL DEFAULT false;

                -- Copy existing global flag to all LOB columns, then drop it
                UPDATE rating_settings SET
                    shadow_mode_gl = shadow_mode_enabled,
                    shadow_mode_im = shadow_mode_enabled,
                    shadow_mode_al = shadow_mode_enabled,
                    shadow_mode_apd = shadow_mode_enabled;

                ALTER TABLE rating_settings DROP COLUMN IF EXISTS shadow_mode_enabled;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE rating_settings
                    ADD COLUMN IF NOT EXISTS shadow_mode_enabled boolean NOT NULL DEFAULT false;
                UPDATE rating_settings SET shadow_mode_enabled = shadow_mode_im;
                ALTER TABLE rating_settings
                    DROP COLUMN IF EXISTS shadow_mode_gl,
                    DROP COLUMN IF EXISTS shadow_mode_im,
                    DROP COLUMN IF EXISTS shadow_mode_al,
                    DROP COLUMN IF EXISTS shadow_mode_apd;
            ");
        }
    }
}
