using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(12)]
public class AddSharepointFileIdTable : Migration
{
    public override void Up()
    {
        Create.Table("sharepoint_fileid").InSchema("public")
            .WithColumn("file_id").AsGuid()
            .WithColumn("dms_file_path").AsString()
            .WithColumn("process_run_id").AsInt32();        
    }

    public override void Down()
    {
        Delete.Table("sharepoint_fileid");
    }
}