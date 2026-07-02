using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(43)]
public class AddColumnToProcessRunTable : Migration
{
    public override void Up()
    {
        Alter.Table("process_run")
            .AddColumn("status").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("status").FromTable("process_run");
    }
}