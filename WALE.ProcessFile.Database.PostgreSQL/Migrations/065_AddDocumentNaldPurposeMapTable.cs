using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(65)]
public class AddDocumentNaldPurposeMapTable : Migration
{
    public override void Up()
    {
        Create.Table("document_nald_purpose_map")
            .WithColumn("document_purpose").AsString().NotNullable()
            .WithColumn("nald_purpose_primary_category_code").AsString().Nullable()
            .WithColumn("nald_purpose_secondary_category_code").AsString().Nullable()
            .WithColumn("nald_purpose_use_code").AsInt32().Nullable()
            .WithColumn("match_type").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Table("document_nald_purpose_map");
    }
}