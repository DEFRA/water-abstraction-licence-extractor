using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(48)]
public class AddIndexForLicenceStatusColumn : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                        CREATE INDEX ix_licence_process_run_id_status_licence_id
                        ON public.licence (
                            process_run_id,
                            status,
                            licence_id
                        );
                    """);
    }

    public override void Down()
    {
        Execute.Sql("""
                        DROP INDEX IF EXISTS public.ix_licence_process_run_id_status_licence_id;
                    """);
    }
}