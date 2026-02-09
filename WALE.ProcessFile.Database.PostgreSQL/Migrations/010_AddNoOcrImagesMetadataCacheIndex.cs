using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(10)]
public class AddNoOcrImagesMetadataCacheIndex : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE INDEX idx_no_ocr_images_metadata_cache ON no_ocr_images_metadata_cache(filename, no_ocr_service_name);");
    }

    public override void Down()
    {
        Execute.Sql("DROP INDEX IF EXISTS idx_no_ocr_images_metadata_cache");
    }
}