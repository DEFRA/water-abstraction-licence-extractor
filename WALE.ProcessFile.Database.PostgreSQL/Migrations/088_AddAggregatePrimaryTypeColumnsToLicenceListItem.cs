using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(88)]
public class AddAggregatePrimaryTypeColumnsToLicenceListItem : Migration
{
    private const string LicenceListItemTable = "licence_list_item";

    public override void Up()
    {
        Alter.Table(LicenceListItemTable)
            .AddColumn("has_in_licence_aggregate")
            .AsBoolean()
            .NotNullable()
            .WithDefaultValue(false)
            .AddColumn("has_licence_to_licence_aggregate")
            .AsBoolean()
            .NotNullable()
            .WithDefaultValue(false);
    }

    public override void Down()
    {
        Delete.Column("has_licence_to_licence_aggregate")
            .FromTable(LicenceListItemTable);

        Delete.Column("has_in_licence_aggregate")
            .FromTable(LicenceListItemTable);
    }
}
