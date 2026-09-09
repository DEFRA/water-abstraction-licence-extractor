using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(68)]
public class AddLicenceIdColumnToLicenceListItem : Migration
{
    public override void Up()
    {
        Alter.Table("licence_list_item")
            .AddColumn("licence_id")
            .AsInt32()
            .Nullable();
    }

    public override void Down()
    {
        Delete.Column("licence_id").FromTable("licence_list_item");
    }
}