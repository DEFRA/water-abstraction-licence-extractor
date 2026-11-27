using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(100)]
public class AddLicenceTypeColumnToVersionFilesToDownloadResult : Migration
{
    private const string VersionFilesToDownloadTable = "version_files_to_download";

    public override void Up()
    {
        // description stays as the generated record of how the run was invoked - the region and
        // file filters are stored nowhere else - so a user-supplied name goes in its own column.
        Alter.Table(VersionFilesToDownloadTable)
            .AddColumn("licence_type")
            .AsString()
            .Nullable();
    }

    public override void Down()
    {
        Delete.Column("licence_type")
            .FromTable(VersionFilesToDownloadTable);
    }
}