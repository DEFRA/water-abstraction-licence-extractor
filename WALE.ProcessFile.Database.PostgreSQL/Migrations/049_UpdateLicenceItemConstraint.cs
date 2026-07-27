using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

using FluentMigrator;

[Migration(49)]
public sealed class UpdateLicenceItemConstraint : Migration
{
    private const string LicenceListItemTable =
        "licence_list_item";
    public override void Up()
    {
        Create.UniqueConstraint(
                "uq_lic_list_process_run_file_licence_number")
            .OnTable(LicenceListItemTable)
            .Columns(
                "process_run_id",
                "file_id", "licence_number");
    }

    public override void Down()
    {
        Delete.UniqueConstraint("uq_lic_list_process_run_file_licence_number");
    }
}