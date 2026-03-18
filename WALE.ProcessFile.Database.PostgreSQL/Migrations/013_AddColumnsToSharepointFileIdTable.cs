using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(13)]
public class AddColumnsToSharepointFileIdTable : Migration
{
    public override void Up()
    {
        Alter.Table("sharepoint_fileid")
            .AddColumn("change_since_previous").AsString().Nullable()
            .AddColumn("first_seen_utc").AsDateTime();      
    }

    public override void Down()
    {
        Delete.Column("change_since_previous").FromTable("sharepoint_fileid");
        Delete.Column("first_seen_utc").FromTable("sharepoint_fileid");
    }
}