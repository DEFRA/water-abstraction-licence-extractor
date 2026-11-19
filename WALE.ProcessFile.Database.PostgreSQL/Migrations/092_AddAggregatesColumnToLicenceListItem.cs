using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(92)]
public class AddAggregatesColumnToLicenceListItem : Migration
{
    private const string LicenceListItemTable = "licence_list_item";

    public override void Up()
    {
        Alter.Table(LicenceListItemTable)
            .AddColumn("aggregates")
            .AsCustom("text[]")
            .Nullable();
    }

    public override void Down()
    {
        Delete.Column("aggregates")
            .FromTable(LicenceListItemTable);
    }
}