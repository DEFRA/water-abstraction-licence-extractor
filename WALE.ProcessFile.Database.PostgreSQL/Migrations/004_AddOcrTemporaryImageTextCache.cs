using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(4)]
public class AddOcrTemporaryImageTextCache : Migration
{
    public override void Up()
    {
        Create.Table("ocr_temporary_image_text_cache")
            .WithColumn("ocr_temporary_image_text_cache_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("page_number").AsInt32().NotNullable()
            .WithColumn("image_number").AsInt32().NotNullable()
            .WithColumn("ocr_service_name").AsString().NotNullable()
            .WithColumn("data").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();
        
        Create.Table("ocr_temporary_screenshot_text_cache")
            .WithColumn("ocr_temporary_image_text_cache_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("page_number").AsInt32().NotNullable()
            .WithColumn("ocr_service_name").AsString().NotNullable()
            .WithColumn("data").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("ocr_temporary_image_text_cache");
        Delete.Table("ocr_temporary_screenshot_text_cache");        
    }
}