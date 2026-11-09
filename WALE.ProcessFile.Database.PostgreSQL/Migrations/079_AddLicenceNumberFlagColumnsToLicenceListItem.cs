using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(79)]
public class AddLicenceNumberFlagColumnsToLicenceListItem : Migration
{
    private const string LicenceListItemTable = "licence_list_item";

    public override void Up()
    {
        Alter.Table(LicenceListItemTable)
            .AddColumn("is_licence_number_flagged")
            .AsBoolean()
            .NotNullable()
            .WithDefaultValue(false)
            .AddColumn("licence_number_flag_reason")
            .AsCustom("text")
            .Nullable();

        // Supports looking up the most recent previous process run in which a licence number appeared
        Execute.Sql("""
                        CREATE INDEX ix_licence_number_process_run_ok
                        ON public.licence (
                            licence_number,
                            process_run_id DESC
                        )
                        INCLUDE (file_id)
                        WHERE (data::jsonb ->> 'status') = 'Ok';
                    """);
    }

    public override void Down()
    {
        Execute.Sql("""
                        DROP INDEX IF EXISTS public.ix_licence_number_process_run_ok;
                    """);

        Delete.Column("licence_number_flag_reason")
            .FromTable(LicenceListItemTable);

        Delete.Column("is_licence_number_flagged")
            .FromTable(LicenceListItemTable);
    }
}
