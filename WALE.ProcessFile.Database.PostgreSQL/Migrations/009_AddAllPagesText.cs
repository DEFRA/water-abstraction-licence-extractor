using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(9)]
public class AddAllPagesText : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE INDEX idx_all_pages_text ON all_pages_text(filename, no_ocr_service_name);");
    }

    public override void Down()
    {
        Execute.Sql("DROP INDEX IF EXISTS idx_all_pages_text");
    }
}