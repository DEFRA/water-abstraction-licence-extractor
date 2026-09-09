using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(64)]
public class AddDocumentNaldPurposeMatchTable : Migration
{
    public override void Up()
    {
        Create.Table("document_nald_purpose_match")
            .WithColumn("lic_no").AsString()
            .WithColumn("document_purpose").AsString().NotNullable()
            .WithColumn("nald_purpose_primary_category_code").AsString().Nullable()
            .WithColumn("nald_purpose_secondary_category_code").AsString().Nullable()
            .WithColumn("nald_purpose_use_code").AsInt32().Nullable()
            .WithColumn("match_type").AsString().Nullable()
            .WithColumn("date_time_utc").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Table("document_nald_purpose_match");
    }
}