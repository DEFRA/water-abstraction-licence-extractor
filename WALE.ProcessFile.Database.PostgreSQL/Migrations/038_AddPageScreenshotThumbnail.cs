using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(38)]
public class AddPageScreenshotThumbnail : Migration
{
    public override void Up()
    {
        Create.Table("page_screenshot_thumbnail")
            .WithColumn("page_screenshot_thumbnail_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("file_id").AsGuid().NotNullable()
            .WithColumn("page_number").AsInt32().NotNullable()
            .WithColumn("no_ocr_service_name").AsString().NotNullable()
            .WithColumn("data").AsBinary(int.MaxValue).NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("page_screenshot_thumbnail");
    }
}