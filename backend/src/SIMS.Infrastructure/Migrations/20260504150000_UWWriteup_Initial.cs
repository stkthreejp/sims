using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMS.Infrastructure.Migrations
{
    public partial class UWWriteup_Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- New columns on insureds
                ALTER TABLE insureds
                    ADD COLUMN IF NOT EXISTS operation_type varchar(200),
                    ADD COLUMN IF NOT EXISTS credit_score integer,
                    ADD COLUMN IF NOT EXISTS website varchar(500);

                -- UW writeup (one per quote)
                CREATE TABLE IF NOT EXISTS quote_uw_writeups (
                    id uuid NOT NULL DEFAULT gen_random_uuid(),
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    is_deleted boolean NOT NULL DEFAULT false,
                    deleted_at timestamptz,
                    quote_id uuid NOT NULL,
                    status varchar(50) NOT NULL DEFAULT 'Draft',
                    decision varchar(50),
                    payload_json jsonb NOT NULL DEFAULT '{}',
                    schema_version integer NOT NULL DEFAULT 1,
                    submitted_at timestamptz,
                    submitted_by_id uuid,
                    approved_at timestamptz,
                    approved_by_id uuid,
                    CONSTRAINT PK_quote_uw_writeups PRIMARY KEY (id),
                    CONSTRAINT UQ_quote_uw_writeups_quote UNIQUE (quote_id),
                    CONSTRAINT FK_quote_uw_writeups_quote FOREIGN KEY (quote_id)
                        REFERENCES quotes ON DELETE CASCADE,
                    CONSTRAINT FK_quote_uw_writeups_submitted_by FOREIGN KEY (submitted_by_id)
                        REFERENCES users (""Id"") ON DELETE RESTRICT,
                    CONSTRAINT FK_quote_uw_writeups_approved_by FOREIGN KEY (approved_by_id)
                        REFERENCES users (""Id"") ON DELETE RESTRICT
                );

                -- UW writeup conditions
                CREATE TABLE IF NOT EXISTS quote_uw_writeup_conditions (
                    id uuid NOT NULL DEFAULT gen_random_uuid(),
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    is_deleted boolean NOT NULL DEFAULT false,
                    deleted_at timestamptz,
                    writeup_id uuid NOT NULL,
                    text varchar(1000) NOT NULL DEFAULT '',
                    required boolean NOT NULL DEFAULT true,
                    satisfied boolean NOT NULL DEFAULT false,
                    sort_order integer NOT NULL DEFAULT 0,
                    CONSTRAINT PK_quote_uw_writeup_conditions PRIMARY KEY (id),
                    CONSTRAINT FK_uw_writeup_conditions_writeup FOREIGN KEY (writeup_id)
                        REFERENCES quote_uw_writeups (id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS IX_quote_uw_writeup_conditions_writeup_id
                    ON quote_uw_writeup_conditions (writeup_id);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS quote_uw_writeup_conditions;
                DROP TABLE IF EXISTS quote_uw_writeups;
                ALTER TABLE insureds
                    DROP COLUMN IF EXISTS operation_type,
                    DROP COLUMN IF EXISTS credit_score,
                    DROP COLUMN IF EXISTS website;
            ");
        }
    }
}
