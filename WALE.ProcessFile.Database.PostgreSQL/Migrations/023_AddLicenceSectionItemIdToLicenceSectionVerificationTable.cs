using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(23)]
public class AddLicenceSectionItemIdToLicenceSectionVerificationTable : Migration
{
    public override void Up()
    {
        Alter.Table("licence_section_verification")
            .AddColumn("licence_section_item_id").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("licence_section_item_id").FromTable("licence_section_verification");
    }
}
