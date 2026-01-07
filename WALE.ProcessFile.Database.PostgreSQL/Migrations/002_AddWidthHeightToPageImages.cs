using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(2)]
public class AddWidthHeightToPageImages : Migration
{
    public override void Up()
    {
        Alter.Table("image_on_page")
            .AddColumn("width").AsInt32().Nullable()
            .AddColumn("height").AsInt32().Nullable();
    }

    public override void Down()
    {
        Delete.Column("width").FromTable("image_on_page");
    }
}