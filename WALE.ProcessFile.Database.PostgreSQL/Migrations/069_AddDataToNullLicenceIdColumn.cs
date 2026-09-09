using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(69)]
public class AddDataToNullLicenceIdColumn : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                    update public.licence_list_item
                    set licence_id = (select licence_id from public.licence where public.licence_list_item.process_run_id = public.licence.process_run_id and public.licence_list_item.file_id = public.licence.file_id LIMIT 1)
                    WHERE licence_id is null and file_id is not null
                    """);
    }

    public override void Down()
    {

    }
}