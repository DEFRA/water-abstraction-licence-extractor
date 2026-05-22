using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(27)]
public class AddVersionFilesToDownloadTable : Migration
{
    public override void Up()
    {
        Create.Table("version_files_to_download").InSchema("public")
            .WithColumn("permit_number").AsString().Nullable()
            .WithColumn("full_path").AsString().Nullable()
            .WithColumn("site_path").AsString().Nullable()
            .WithColumn("library_and_file_path").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Table("version_files_to_download");
    }
}