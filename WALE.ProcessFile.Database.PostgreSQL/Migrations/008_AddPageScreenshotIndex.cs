using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(8)]
public class AddPageScreenshotIndex : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE INDEX idx_page_screenshot ON page_screenshot(filename, page_number, no_ocr_service_name);");
    }

    public override void Down()
    {
        Execute.Sql("DROP INDEX IF EXISTS idx_page_screenshot");
    }
}