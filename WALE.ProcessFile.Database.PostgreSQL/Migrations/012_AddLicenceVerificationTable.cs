using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(12)]
public class AddLicenceVerificationTable : Migration
{
    public override void Up()
    {
        Create.Table("licence_section_verification")
            .WithColumn("licence_section_verification_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("licence_file_id").AsGuid().NotNullable()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("licence_section_name").AsString().NotNullable()
            .WithColumn("licence_section_value").AsCustom("jsonb").Nullable()
            .WithColumn("verification_type").AsString().NotNullable()
            .WithColumn("created_date_time_utc").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("licence_section_verification");
    }
}
