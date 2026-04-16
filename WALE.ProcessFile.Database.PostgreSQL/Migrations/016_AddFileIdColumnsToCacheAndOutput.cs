using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(16)]
public class AddFileIdColumnsToCacheAndOutput : Migration
{
    public override void Up()
    {
        Alter.Table("all_pages_text")
            .AddColumn("file_id").AsGuid().Nullable();
        
        Alter.Table("image_on_page")
            .AddColumn("file_id").AsGuid().Nullable();
        
        Alter.Table("no_ocr_images_metadata_cache")
            .AddColumn("file_id").AsGuid().Nullable();
        
        Alter.Table("no_ocr_pages_metadata_cache")
            .AddColumn("file_id").AsGuid().Nullable();
        
        Alter.Table("no_ocr_page_text_cache")
            .AddColumn("file_id").AsGuid().Nullable();
        
        Alter.Table("ocr_image_text_cache")
            .AddColumn("file_id").AsGuid().Nullable();
        
        Alter.Table("ocr_screenshot_text_cache")
            .AddColumn("file_id").AsGuid().Nullable();
        
        Alter.Table("page_screenshot")
            .AddColumn("file_id").AsGuid().Nullable();        
    }

    public override void Down()
    {
        Delete.Column("file_id").FromTable("all_pages_text");
        Delete.Column("file_id").FromTable("image_on_page");
        Delete.Column("file_id").FromTable("no_ocr_images_metadata_cache");
        Delete.Column("file_id").FromTable("no_ocr_pages_metadata_cache");
        Delete.Column("file_id").FromTable("no_ocr_page_text_cache");
        Delete.Column("file_id").FromTable("ocr_image_text_cache");
        Delete.Column("file_id").FromTable("ocr_screenshot_text_cache");
        Delete.Column("file_id").FromTable("page_screenshot");
    }
}