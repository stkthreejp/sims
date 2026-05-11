using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GL_RatingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE submission_gl_coverages
                    ADD COLUMN IF NOT EXISTS ai_blanket              boolean     NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS ai_individual_count     integer     NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS include_tria            boolean     NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS primary_non_contributory boolean    NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS wos_blanket             boolean     NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS wos_individual_count    integer     NOT NULL DEFAULT 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE submission_gl_coverages
                    DROP COLUMN IF EXISTS ai_blanket,
                    DROP COLUMN IF EXISTS ai_individual_count,
                    DROP COLUMN IF EXISTS include_tria,
                    DROP COLUMN IF EXISTS primary_non_contributory,
                    DROP COLUMN IF EXISTS wos_blanket,
                    DROP COLUMN IF EXISTS wos_individual_count;
            ");
        }
    }
}
