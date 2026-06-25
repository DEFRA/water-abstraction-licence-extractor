using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(22)]
public class AddNotesToLicenceSectionVerificationTable : Migration
{
    public override void Up()
    {
        Alter.Table("licence_section_verification")
            .AddColumn("notes").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("notes").FromTable("licence_section_verification");
    }
}
