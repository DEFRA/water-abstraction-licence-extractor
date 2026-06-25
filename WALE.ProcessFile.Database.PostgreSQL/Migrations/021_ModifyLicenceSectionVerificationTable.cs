using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(21)]
public class ModifyLicenceSectionVerificationTable : Migration
{
    public override void Up()
    {
        Alter.Table("licence_section_verification")
            .AddColumn("licence_section_scraped_value").AsCustom("jsonb").Nullable()
            .AddColumn("licence_section_override_value").AsCustom("jsonb").Nullable();

        Delete.Column("licence_section_value").FromTable("licence_section_verification");
    }

    public override void Down()
    {
        Alter.Table("licence_section_verification")
            .AddColumn("licence_section_value").AsCustom("jsonb").Nullable();

        Delete.Column("licence_section_scraped_value").FromTable("licence_section_verification");
        Delete.Column("licence_section_override_value").FromTable("licence_section_verification");
    }
}
