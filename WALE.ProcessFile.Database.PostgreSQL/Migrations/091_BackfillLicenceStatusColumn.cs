using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(91)]
public class BackfillLicenceStatusColumn : Migration
{
    public override void Up()
    {
        // 047 added the column with no backfill, so rows written before it carry their status
        // only in the jsonb blob. A row whose data will not cast keeps its null rather than
        // failing the migration; pg_input_is_valid needs Postgres 16.
        Execute.Sql("""
                        UPDATE licence
                        SET status = data::jsonb ->> 'status'
                        WHERE status IS NULL
                          AND data IS NOT NULL
                          AND pg_input_is_valid(data, 'jsonb');
                    """);
    }

    // The filled rows cannot be told from the rest afterwards, so this does not reverse.
    public override void Down()
    {
    }
}
