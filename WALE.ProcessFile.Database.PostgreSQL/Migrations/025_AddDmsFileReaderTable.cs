using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(25)]
public class AddDmsFileReaderTable : Migration
{
    public override void Up()
    {
        Create.Table("dms_file_reader").InSchema("public")
            .WithColumn("status").AsString().Nullable()
            .WithColumn("error_message").AsString().Nullable()
            .WithColumn("licence_number").AsString().Nullable()
            .WithColumn("permit_number").AsString().Nullable()
            .WithColumn("file_name").AsString().NotNullable()
            .WithColumn("original_file_name").AsString().Nullable()
            .WithColumn("file_id").AsGuid().Nullable().NotNullable()
            .WithColumn("date_of_issue").AsDateTime().Nullable()
            .WithColumn("number_of_pages").AsInt32().Nullable()
            .WithColumn("primary_type").AsString().Nullable()
            .WithColumn("secondary_type").AsString().Nullable()
            .WithColumn("file_type").AsString().Nullable()
            .WithColumn("confidence").AsDouble().Nullable()
            .WithColumn("identified_by_rule").AsString().Nullable()
            .WithColumn("matched_terms").AsString().Nullable()
            .WithColumn("file_size").AsInt64().Nullable();
    }

    public override void Down()
    {
        Delete.Table("dms_file_reader");
    }
}