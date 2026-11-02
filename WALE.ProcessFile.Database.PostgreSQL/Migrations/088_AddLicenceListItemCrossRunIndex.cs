using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(88)]
public sealed class AddLicenceListItemCrossRunIndex : Migration
{
    public override void Up()
    {
        // Must match AddLicenceNumbersFilter's lower(licence_number) - an index on the bare
        // column does not serve it.
        Execute.Sql("""
                        CREATE INDEX ix_lic_list_number_run
                        ON licence_list_item (
                            lower(licence_number),
                            process_run_id DESC
                        );
                    """);
    }

    public override void Down()
    {
        Execute.Sql("""
                        DROP INDEX IF EXISTS ix_lic_list_number_run;
                    """);
    }
}
