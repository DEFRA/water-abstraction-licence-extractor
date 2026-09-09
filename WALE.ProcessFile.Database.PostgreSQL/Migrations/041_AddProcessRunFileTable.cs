using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(41)]
public class AddProcessRunFileTable : Migration
{
    public override void Up()
    {
        Create.Table("process_run_file")
            .WithColumn("process_run__file_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("file_name").AsString().Nullable()
            .WithColumn("end_date_time_utc").AsDateTime().Nullable()
            .WithColumn("error_message").AsString().Nullable()
            .WithColumn("start_date_time_utc").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Table("process_run_file");
    }
}