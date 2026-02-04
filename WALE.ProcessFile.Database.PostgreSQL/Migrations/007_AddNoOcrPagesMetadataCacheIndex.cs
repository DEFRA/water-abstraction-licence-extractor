using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(7)]
public class AddNoOcrPagesMetadataCacheIndex : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE INDEX idx_no_ocr_pages_metadata_cache_filename_no_ocr_service_name ON no_ocr_pages_metadata_cache(filename, no_ocr_service_name);");
    }

    public override void Down()
    {
        Execute.Sql("DROP INDEX IF EXISTS idx_no_ocr_pages_metadata_cache_filename_no_ocr_service_name");
    }
}