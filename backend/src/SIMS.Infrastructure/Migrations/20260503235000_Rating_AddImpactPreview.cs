using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    public partial class Rating_AddImpactPreview : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS rating_plan_version_impact_previews (
                    id uuid NOT NULL DEFAULT gen_random_uuid(),
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    is_deleted boolean NOT NULL DEFAULT false,
                    deleted_at timestamptz,
                    rating_plan_version_id uuid NOT NULL,
                    computed_at timestamptz NOT NULL,
                    computed_by_id uuid NOT NULL,
                    quote_count integer NOT NULL DEFAULT 0,
                    total_current_premium numeric(18,2) NOT NULL DEFAULT 0,
                    total_new_premium numeric(18,2) NOT NULL DEFAULT 0,
                    total_delta_pct numeric(18,4) NOT NULL DEFAULT 0,
                    quotes_up integer NOT NULL DEFAULT 0,
                    quotes_down integer NOT NULL DEFAULT 0,
                    quotes_flat integer NOT NULL DEFAULT 0,
                    preview_json jsonb NOT NULL DEFAULT '{}',
                    CONSTRAINT PK_rating_plan_version_impact_previews PRIMARY KEY (id),
                    CONSTRAINT FK_impact_previews_versions FOREIGN KEY (rating_plan_version_id)
                        REFERENCES rating_plan_versions (id) ON DELETE CASCADE,
                    CONSTRAINT FK_impact_previews_users FOREIGN KEY (computed_by_id)
                        REFERENCES users (""Id"") ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS IX_impact_previews_version_id
                    ON rating_plan_version_impact_previews (rating_plan_version_id);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS rating_plan_version_impact_previews;");
        }
    }
}
