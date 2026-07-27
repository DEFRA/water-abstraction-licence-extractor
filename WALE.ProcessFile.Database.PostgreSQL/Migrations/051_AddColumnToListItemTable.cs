using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(51)]
public class AddColumnToListItemTable : Migration
{
    private const string LicenceListItemTable =
        "licence_list_item";
    public override void Up()
    {
        Alter.Table(LicenceListItemTable)
            .AddColumn("purposes")
            .AsCustom("text")
            .Nullable();
        
        Alter.Table(LicenceListItemTable)
            .AddColumn("points")
            .AsCustom("text")
            .Nullable(); 
    }

    public override void Down()
    {
        Delete.Column("points").FromTable(LicenceListItemTable);
        Delete.Column("purposes").FromTable(LicenceListItemTable);
    }
}