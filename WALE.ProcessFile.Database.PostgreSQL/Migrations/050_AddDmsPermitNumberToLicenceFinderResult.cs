using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(50)]
public class AddDmsPermitNumberToLicenceFinderResult : Migration
{
    public override void Up()
    {
        Alter.Table("licence_finder_result")
            .AddColumn("dms_permit_number").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("dms_permit_number").FromTable("licence_finder_result");
    }
}