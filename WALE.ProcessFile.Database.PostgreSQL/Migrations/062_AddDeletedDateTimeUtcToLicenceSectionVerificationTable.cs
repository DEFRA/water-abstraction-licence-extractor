using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(62)]
public class AddDeletedDateTimeUtcToLicenceSectionVerificationTable : Migration
{
    public override void Up()
    {
        Alter.Table("licence_section_verification")
            .AddColumn("deleted_date_time_utc").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Column("deleted_date_time_utc").FromTable("licence_section_verification");
    }
}
