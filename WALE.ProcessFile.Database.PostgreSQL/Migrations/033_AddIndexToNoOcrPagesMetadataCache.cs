using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(33)]
public class AddIndexToNoOcrPagesMetadataCache  : Migration
{
    public override void Up()
    {
        Create.Index("idx_no_ocr_pages_metadata_cache")
            .OnTable("no_ocr_pages_metadata_cache")
            .OnColumn("file_id").Ascending()
            .OnColumn("no_ocr_service_name").Ascending();
    }

    public override void Down()
    {
        Delete.Index("idx_no_ocr_pages_metadata_cache").OnTable("no_ocr_pages_metadata_cache");
    }
}