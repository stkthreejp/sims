using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    public partial class Rating_AddShadowRatingResults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS shadow_rating_results (
                    id uuid NOT NULL DEFAULT gen_random_uuid(),
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    is_deleted boolean NOT NULL DEFAULT false,
                    deleted_at timestamptz,
                    quote_id uuid NOT NULL,
                    rating_plan_version_id uuid NOT NULL,
                    rated_at timestamptz NOT NULL,
                    rated_by_id uuid NOT NULL,
                    shadow_premium numeric(18,2) NOT NULL DEFAULT 0,
                    actual_premium numeric(18,2) NOT NULL DEFAULT 0,
                    delta_amount numeric(18,2) NOT NULL DEFAULT 0,
                    delta_pct numeric(18,4) NOT NULL DEFAULT 0,
                    schedule_modifier numeric(6,4) NOT NULL DEFAULT 1,
                    snapshot_json jsonb NOT NULL DEFAULT '{}',
                    CONSTRAINT PK_shadow_rating_results PRIMARY KEY (id),
                    CONSTRAINT FK_shadow_results_quotes FOREIGN KEY (quote_id)
                        REFERENCES quotes ON DELETE RESTRICT,
                    CONSTRAINT FK_shadow_results_versions FOREIGN KEY (rating_plan_version_id)
                        REFERENCES rating_plan_versions (id) ON DELETE RESTRICT,
                    CONSTRAINT FK_shadow_results_users FOREIGN KEY (rated_by_id)
                        REFERENCES users (""Id"") ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS IX_shadow_rating_results_quote_id
                    ON shadow_rating_results (quote_id);
                CREATE INDEX IF NOT EXISTS IX_shadow_rating_results_rated_at
                    ON shadow_rating_results (rated_at);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS shadow_rating_results;");
        }
    }
}
