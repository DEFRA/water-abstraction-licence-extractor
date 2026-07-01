using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(44)]
public class IndexesForProcessRunLicenceStatusSearch : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                        CREATE INDEX ix_licence_process_run_status_licence_id
                        ON public.licence (
                            process_run_id,
                            ((data::jsonb ->> 'status')),
                            licence_id
                        );
                    """);
    }

    public override void Down()
    {
        Execute.Sql("""
                        DROP INDEX IF EXISTS public.ix_licence_process_run_status_licence_id;
                    """);
    }
}