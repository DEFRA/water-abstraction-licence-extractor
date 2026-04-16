using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(18)]
public class AddFileIdAndMakeFilenameNullableInCacheTemporaryTables : Migration
{
    public override void Up()
    {
        Alter.Table("ocr_temporary_image_text_cache")
            .AddColumn("file_id").AsGuid().Nullable();
        
        Alter.Table("ocr_temporary_screenshot_text_cache")
            .AddColumn("file_id").AsGuid().Nullable();
        
        Alter.Table("ocr_temporary_image_text_cache")
            .AlterColumn("filename").AsString().Nullable();
        
        Alter.Table("ocr_temporary_screenshot_text_cache")
            .AlterColumn("filename").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("file_id").FromTable("ocr_temporary_image_text_cache");
        Delete.Column("file_id").FromTable("ocr_temporary_screenshot_text_cache");
        
        Alter.Table("ocr_temporary_image_text_cache")
            .AlterColumn("filename").AsString().NotNullable();
        
        Alter.Table("ocr_temporary_screenshot_text_cache")
            .AlterColumn("filename").AsString().NotNullable();
    }
}