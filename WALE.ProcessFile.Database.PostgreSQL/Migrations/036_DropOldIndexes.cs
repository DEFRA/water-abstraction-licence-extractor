using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(36)]
public class DropOldIndexes : Migration
{
    public override void Up()
    {
        Delete.Index("idx_no_ocr_page_text_cache_filename_page_number_no_ocr_service_name").OnTable("no_ocr_page_text_cache");
        Delete.Index("idx_no_ocr_pages_metadata_cache_filename_no_ocr_service_name").OnTable("no_ocr_pages_metadata_cache");
        Delete.Index("idx_page_screenshot").OnTable("page_screenshot");
        Delete.Index("idx_all_pages_text").OnTable("all_pages_text");
        Delete.Index("idx_no_ocr_images_metadata_cache").OnTable("no_ocr_images_metadata_cache");
    }

    public override void Down()
    {
    }
}