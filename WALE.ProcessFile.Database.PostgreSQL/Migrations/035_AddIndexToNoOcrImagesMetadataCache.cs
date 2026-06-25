using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(35)]
public class AddIndexToNoOcrImagesMetadataCache : Migration
{
    public override void Up()
    {
        Create.Index("idx_no_ocr_images_metadata_cache_fileid")
            .OnTable("no_ocr_images_metadata_cache")
            .OnColumn("file_id").Ascending()
            .OnColumn("no_ocr_service_name").Ascending();
    }

    public override void Down()
    {
        Delete.Index("idx_no_ocr_images_metadata_cache_fileid").OnTable("no_ocr_images_metadata_cache");
    }
}