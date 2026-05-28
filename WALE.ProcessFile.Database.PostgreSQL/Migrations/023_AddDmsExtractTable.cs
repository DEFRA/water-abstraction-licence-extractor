using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(23)]
public class AddDmsExtractTable : Migration
{
    public override void Up()
    {
        Create.Table("dms_extract").InSchema("public")
            .WithColumn("site_collection").AsString().Nullable()
            .WithColumn("library_name").AsString().Nullable()
            .WithColumn("permit_number").AsString().Nullable()
            .WithColumn("file_name").AsString().Nullable()
            .WithColumn("file_size").AsString().Nullable()
            .WithColumn("file_type").AsString().Nullable()
            .WithColumn("customer_operator_name").AsString().Nullable()
            .WithColumn("facility_name").AsString().Nullable()
            .WithColumn("facility_address").AsString().Nullable()
            .WithColumn("facility_address_postcode").AsString().Nullable()
            .WithColumn("regime").AsString().Nullable()
            .WithColumn("activity_class").AsString().Nullable()
            .WithColumn("activity_sub_class").AsString().Nullable()
            .WithColumn("type_of_permit").AsString().Nullable()
            .WithColumn("catchment").AsString().Nullable()
            .WithColumn("national_security").AsString().Nullable()
            .WithColumn("disclosure_status").AsString().Nullable()
            .WithColumn("document_date").AsString().Nullable()
            .WithColumn("upload_date").AsString().Nullable()
            .WithColumn("file_url").AsString().Nullable()
            .WithColumn("other_reference").AsString().Nullable()
            .WithColumn("modified_date").AsString().Nullable()
            .WithColumn("file_id").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Table("dms_extract");
    }
}