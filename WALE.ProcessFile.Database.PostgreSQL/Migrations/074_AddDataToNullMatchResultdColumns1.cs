using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(74)]
public class AddDataToNullMatchResultdColumns1 : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                    update public.licence
                    set matches_result_id = (select matches_result_id from public.matches_result where public.licence.process_run_id = public.matches_result.process_run_id and public.licence.file_id = public.matches_result.file_id LIMIT 1)
                    WHERE
                        (matches_result_id is null or matches_result_id = 0)
                        and file_id is not null
                    """);
    }

    public override void Down()
    {

    }
}