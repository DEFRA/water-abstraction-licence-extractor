using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(59)]
public sealed class AddNewContainedInColumnsToLicenceListItemLinkLocation : Migration
{
    public override void Up()
    {
        Alter.Table("licence_list_item_link_location")
            .AddColumn("acin_code").AsString().Nullable();
        
        Alter.Table("licence_list_item_link_location")
            .AddColumn("source_fields").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("acin_code").FromTable("licence_list_item_link_location");
        Delete.Column("source_fields").FromTable("licence_list_item_link_location");
    }
}