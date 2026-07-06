using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(42)]
public class AddConstraintProcessRunFileTable : Migration
{
    public override void Up()
    {
        Create.UniqueConstraint("uq_process_run_file_process_run_id_file_name")
            .OnTable("process_run_file")
            .Columns("process_run_id", "file_name");
    }

    public override void Down()
    {
        Delete.UniqueConstraint("uq_process_run_file_process_run_id_file_name")
            .FromTable("process_run_file");
    }
}