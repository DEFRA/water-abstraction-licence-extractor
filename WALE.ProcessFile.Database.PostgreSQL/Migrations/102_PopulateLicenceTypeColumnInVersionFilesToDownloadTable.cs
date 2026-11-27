using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(102)]
public class PopulateLicenceTypeColumnInVersionFilesToDownloadTable : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                        UPDATE version_files_to_download
                        SET licence_type = 'Abstraction'
                        WHERE licence_type is null
                    """);
    }

    public override void Down()
    {
        Execute.Sql("""
                        UPDATE version_files_to_download
                        SET licence_type = null
                    """);
    }
}