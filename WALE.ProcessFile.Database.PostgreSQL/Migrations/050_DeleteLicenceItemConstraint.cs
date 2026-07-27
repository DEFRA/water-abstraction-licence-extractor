using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

using FluentMigrator;

[Migration(50)]
public sealed class DeleteLicenceItemConstraint : Migration
{
    private const string LicenceListItemTable =
        "licence_list_item";
    public override void Up()
    {
        Delete.UniqueConstraint(
                "uq_lic_list_process_run_file").FromTable(LicenceListItemTable);
    }

    public override void Down()
    {
        Create.UniqueConstraint(
                "uq_lic_list_process_run_file")
            .OnTable(LicenceListItemTable)
            .Columns(
                "process_run_id",
                "file_id");
    }
}