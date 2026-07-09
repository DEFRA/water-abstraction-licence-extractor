using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(45)]
public class AddSuccessCountColumnToProcessRunTable : Migration
{
    public override void Up()
    {
        Alter.Table("process_run")
            .AddColumn("success_count").AsInt32().Nullable();
    }

    public override void Down()
    {
        Delete.Column("success_count").FromTable("process_run");
    }
}