using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(14)]
public class ChangeColumnsInSharepointFileIdTable : Migration
{
    public override void Up()
    {
        Delete.Column("change_since_previous").FromTable("sharepoint_fileid");
        Delete.Column("first_seen_utc").FromTable("sharepoint_fileid");
        
        Alter.Table("sharepoint_fileid")
            .AddColumn("status").AsString().Nullable()
            .AddColumn("status_date_utc").AsDateTime();
    }

    public override void Down()
    {
        Alter.Table("sharepoint_fileid")
            .AddColumn("change_since_previous").AsString().Nullable()
            .AddColumn("first_seen_utc").AsDateTime();
        
        Delete.Column("status").FromTable("sharepoint_fileid");
        Delete.Column("status_date_utc").FromTable("sharepoint_fileid");
    }
}