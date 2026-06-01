using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(29)]
public class AddColumnsToVersionFilesTable : Migration
{
    public override void Up()
    {
        Alter.Table("version_files")
            .AddColumn("file_name").AsString().Nullable()
            .AddColumn("file_id").AsGuid().Nullable()
            .AddColumn("file_size").AsInt32().Nullable();
    }

    public override void Down()
    {
        Delete.Column("file_name").FromTable("version_files");
        Delete.Column("file_id").FromTable("version_files");
        Delete.Column("file_size").FromTable("version_files");
    }
}