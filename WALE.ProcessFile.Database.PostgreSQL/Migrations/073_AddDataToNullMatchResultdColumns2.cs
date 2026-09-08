using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(73)]
public class AddDataToNullMatchResultdColumns2 : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                    update public.licence_list_item
                    set matches_result_id = (select matches_result_id from public.matches_result where public.licence_list_item.process_run_id = public.matches_result.process_run_id and public.licence_list_item.file_id = public.matches_result.file_id LIMIT 1)
                    WHERE
                        matches_result_id is null
                        and file_id is not null
                    """);
    }

    public override void Down()
    {

    }
}