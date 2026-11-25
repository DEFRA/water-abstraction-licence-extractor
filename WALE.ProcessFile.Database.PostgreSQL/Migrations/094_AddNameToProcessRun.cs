using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(94)]
public class AddNameToProcessRun : Migration
{
    private const string ProcessRunTable = "process_run";

    public override void Up()
    {
        // description stays as the generated record of how the run was invoked - the region and
        // file filters are stored nowhere else - so a user-supplied name goes in its own column.
        Alter.Table(ProcessRunTable)
            .AddColumn("name")
            .AsString()
            .Nullable();
    }

    public override void Down()
    {
        Delete.Column("name")
            .FromTable(ProcessRunTable);
    }
}
