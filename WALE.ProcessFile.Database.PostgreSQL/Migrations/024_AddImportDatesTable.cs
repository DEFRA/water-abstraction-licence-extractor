using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(24)]
public class AddImportDatesTable : Migration
{
    public override void Up()
    {
        Create.Table("import_dates").InSchema("public")
            .WithColumn("data_source").AsString().NotNullable()
            .WithColumn("date_time").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("import_dates");
    }
}