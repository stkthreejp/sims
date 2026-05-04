using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    public partial class Rating_AddRatingSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS rating_settings (
                    id uuid NOT NULL DEFAULT gen_random_uuid(),
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    is_deleted boolean NOT NULL DEFAULT false,
                    deleted_at timestamptz,
                    shadow_mode_enabled boolean NOT NULL DEFAULT false,
                    CONSTRAINT PK_rating_settings PRIMARY KEY (id)
                );
                -- Seed one row so there's always a settings record to read/update
                INSERT INTO rating_settings (id, shadow_mode_enabled)
                VALUES ('00000000-0000-0000-0000-000000000001', false)
                ON CONFLICT (id) DO NOTHING;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS rating_settings;");
        }
    }
}
