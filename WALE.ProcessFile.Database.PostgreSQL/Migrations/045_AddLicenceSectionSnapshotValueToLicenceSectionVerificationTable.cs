using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(45)]
public class AddLicenceSectionSnapshotValueToLicenceSectionVerificationTable : Migration
{
    public override void Up()
    {
        Alter.Table("licence_section_verification")
            .AddColumn("licence_section_snapshot_value").AsCustom("jsonb").Nullable();
    }

    public override void Down()
    {
        Delete.Column("licence_section_snapshot_value").FromTable("licence_section_verification");
    }
}
