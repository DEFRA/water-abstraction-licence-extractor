using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(34)]
public class AddIndexToNoOcrPageTextCache : Migration
{
    public override void Up()
    {
        Create.Index("idx_no_ocr_page_text_cache")
            .OnTable("no_ocr_page_text_cache")
            .OnColumn("file_id").Ascending()
            .OnColumn("no_ocr_service_name").Ascending();
    }

    public override void Down()
    {
        Delete.Index("idx_no_ocr_page_text_cache").OnTable("no_ocr_page_text_cache");
    }
}