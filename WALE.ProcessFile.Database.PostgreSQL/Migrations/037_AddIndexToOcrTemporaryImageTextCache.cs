using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(37)]
public class AddIndexToOcrTemporaryImageTextCache : Migration
{
    public override void Up()
    {
        Create.Index("idx_ocr_temporary_image_text_cache")
            .OnTable("ocr_temporary_image_text_cache")
            .OnColumn("file_id").Ascending()
            .OnColumn("ocr_service_name").Ascending()
            .OnColumn("page_number").Ascending()
            .OnColumn("image_number").Ascending();
    }

    public override void Down()
    {
        Delete.Index("idx_ocr_temporary_image_text_cache")
            .OnTable("ocr_temporary_image_text_cache");
    }
}