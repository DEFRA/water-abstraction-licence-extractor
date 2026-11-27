using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(98)]
public class PopulateLicenceTypeColumnInLicenceFinderResult : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                        UPDATE licence_finder_result
                        SET licence_type = 'Abstraction'
                        WHERE licence_type is null
                    """);
    }

    public override void Down()
    {
        Execute.Sql("""
                        UPDATE licence_finder_result
                        SET licence_type = null
                    """);
    }
}