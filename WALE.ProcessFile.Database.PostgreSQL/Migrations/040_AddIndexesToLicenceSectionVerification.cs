using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(40)]
public class AddIndexesToLicenceSectionVerification : Migration
{
    public override void Up()
    {
        Create.Index("idx_licence_section_verification_licence_section_item_id")
            .OnTable("licence_section_verification")
            .OnColumn("licence_section_item_id").Ascending();

        Create.Index("idx_licence_section_verification_licence_file_id")
            .OnTable("licence_section_verification")
            .OnColumn("licence_file_id").Ascending();
    }

    public override void Down()
    {
        Delete.Index("idx_licence_section_verification_licence_section_item_id").OnTable("licence_section_verification");
        Delete.Index("idx_licence_section_verification_licence_file_id").OnTable("licence_section_verification");
    }
}
