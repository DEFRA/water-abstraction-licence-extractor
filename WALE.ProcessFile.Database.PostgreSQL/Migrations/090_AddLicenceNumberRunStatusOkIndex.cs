using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(90)]
public class AddLicenceNumberRunStatusOkIndex : Migration
{
    public override void Up()
    {
        // ix_licence_number_process_run_ok (079) has the same shape over the jsonb status, so it
        // cannot serve the status column filter this query now uses. 079's index is left in
        // place only until index-usage counters on a deployed environment confirm it is dead.
        Execute.Sql("""
                        CREATE INDEX ix_licence_number_run_status_ok
                        ON public.licence (
                            licence_number,
                            process_run_id DESC
                        )
                        INCLUDE (file_id)
                        WHERE status = 'Ok';
                    """);
    }

    public override void Down()
    {
        Execute.Sql("""
                        DROP INDEX IF EXISTS public.ix_licence_number_run_status_ok;
                    """);
    }
}
