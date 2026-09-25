using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(77)]
public class AddLicenceThumbnailUrl : Migration
{
    public override void Up()
    {
        Alter.Table("licence")
            .AddColumn("thumbnail_url")
            .AsString()
            .Nullable();
    }

    public override void Down()
    {
        Delete.Column("thumbnail_url").FromTable("licence");
    }
}