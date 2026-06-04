using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(39)]
public class IndexesForScreenshotsAndThumbnails : Migration
{
    public override void Up()
    {
        Create.Index("idx_page_screenshot")
            .OnTable("page_screenshot")
            .OnColumn("file_id").Ascending()
            .OnColumn("page_number").Ascending()            
            .OnColumn("no_ocr_service_name").Ascending();
        
        Create.Index("idx_page_screenshot_thumbnail")
            .OnTable("page_screenshot_thumbnail")
            .OnColumn("file_id").Ascending()
            .OnColumn("page_number").Ascending()            
            .OnColumn("no_ocr_service_name").Ascending();
    }

    public override void Down()
    {
        Delete.Index("idx_page_screenshot").OnTable("page_screenshot");
        Delete.Index("idx_page_screenshot_thumbnail").OnTable("page_screenshot_thumbnail");
    }
}