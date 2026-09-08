using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(71)]
public class AddMatchesResultIdColumnToLicenceListItem : Migration
{
    public override void Up()
    {
        Alter.Table("licence_list_item")
            .AddColumn("matches_result_id")
            .AsInt32()
            .Nullable();
    }

    public override void Down()
    {
        Delete.Column("matches_result_id").FromTable("licence_list_item");
    }
}