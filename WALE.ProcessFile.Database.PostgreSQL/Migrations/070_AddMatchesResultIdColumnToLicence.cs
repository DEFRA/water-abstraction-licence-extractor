using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(70)]
public class AddMatchesResultIdColumnToLicence : Migration
{
    public override void Up()
    {
        Alter.Table("licence")
            .AddColumn("matches_result_id")
            .AsInt32()
            .Nullable();
    }

    public override void Down()
    {
        Delete.Column("matches_result_id").FromTable("licence");
    }
}