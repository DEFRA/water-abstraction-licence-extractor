using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(101)]
public class PopulateLicenceTypeColumnInVersionFileTable : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                        UPDATE version_files
                        SET licence_type = 'Abstraction'
                        WHERE licence_type is null
                    """);
    }

    public override void Down()
    {
        Execute.Sql("""
                        UPDATE version_files
                        SET licence_type = null
                    """);
    }
}