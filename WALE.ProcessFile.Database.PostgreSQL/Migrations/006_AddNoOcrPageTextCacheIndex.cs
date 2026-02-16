using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(6)]
public class AddNoOcrPageTextCacheIndex : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE INDEX idx_no_ocr_page_text_cache_filename_page_number_no_ocr_service_name ON no_ocr_page_text_cache(filename, page_number, no_ocr_service_name);");
    }

    public override void Down()
    {
        Execute.Sql("DROP INDEX IF EXISTS idx_no_ocr_page_text_cache_filename_page_number_no_ocr_service_name");
    }
}