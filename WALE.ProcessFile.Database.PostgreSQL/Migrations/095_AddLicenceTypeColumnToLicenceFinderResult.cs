using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(95)]
public class AddLicenceTypeColumnToLicenceFinderResult : Migration
{
    private const string LicenceFinderResultTable = "licence_finder_result";

    public override void Up()
    {
        // description stays as the generated record of how the run was invoked - the region and
        // file filters are stored nowhere else - so a user-supplied name goes in its own column.
        Alter.Table(LicenceFinderResultTable)
            .AddColumn("licence_type")
            .AsString()
            .Nullable();
    }

    public override void Down()
    {
        Delete.Column("licence_type")
            .FromTable(LicenceFinderResultTable);
    }
}