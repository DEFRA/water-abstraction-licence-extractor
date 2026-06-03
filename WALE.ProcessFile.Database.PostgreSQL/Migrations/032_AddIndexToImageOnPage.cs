using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(32)]
public class AddIndexToImageOnPage : Migration
{
    public override void Up()
    {
        Create.Index("idx_image_on_page")
            .OnTable("image_on_page")
            .OnColumn("file_id").Ascending()
            .OnColumn("page_number").Ascending();
    }

    public override void Down()
    {
        Delete.Index("idx_image_on_page").OnTable("image_on_page");
    }
}