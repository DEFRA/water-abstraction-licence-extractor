using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(17)]
public class MakeFilenameNullableInCacheAndOutput : Migration
{
    public override void Up()
    {
        Alter.Table("all_pages_text")
            .AlterColumn("filename").AsString().Nullable();
        
        Alter.Table("image_on_page")
            .AlterColumn("filename").AsString().Nullable();
        
        Alter.Table("no_ocr_images_metadata_cache")
            .AlterColumn("filename").AsString().Nullable();
        
        Alter.Table("no_ocr_pages_metadata_cache")
            .AlterColumn("filename").AsString().Nullable();
        
        Alter.Table("no_ocr_page_text_cache")
            .AlterColumn("filename").AsString().Nullable();
        
        Alter.Table("ocr_image_text_cache")
            .AlterColumn("filename").AsString().Nullable();
        
        Alter.Table("ocr_screenshot_text_cache")
            .AlterColumn("filename").AsString().Nullable();
        
        Alter.Table("page_screenshot")
            .AlterColumn("filename").AsString().Nullable();
    }

    public override void Down()
    {
        Alter.Table("all_pages_text")
            .AlterColumn("filename").AsString().NotNullable();
        
        Alter.Table("image_on_page")
            .AlterColumn("filename").AsString().NotNullable();
        
        Alter.Table("no_ocr_images_metadata_cache")
            .AlterColumn("filename").AsString().NotNullable();
        
        Alter.Table("no_ocr_pages_metadata_cache")
            .AlterColumn("filename").AsString().NotNullable();
        
        Alter.Table("no_ocr_page_text_cache")
            .AlterColumn("filename").AsString().NotNullable();
        
        Alter.Table("ocr_image_text_cache")
            .AlterColumn("filename").AsString().NotNullable();
        
        Alter.Table("ocr_screenshot_text_cache")
            .AlterColumn("filename").AsString().NotNullable();
        
        Alter.Table("page_screenshot")
            .AlterColumn("filename").AsString().NotNullable();
    }
}