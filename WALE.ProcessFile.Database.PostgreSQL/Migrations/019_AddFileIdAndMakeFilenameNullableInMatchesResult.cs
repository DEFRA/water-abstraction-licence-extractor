using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(19)]
public class AddFileIdAndMakeFilenameNullableInMatchesResult : Migration
{
    public override void Up()
    {
        Alter.Table("matches_result")
            .AddColumn("file_id").AsGuid().Nullable();
        
        Alter.Table("matches_result")
            .AlterColumn("filename").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("file_id").FromTable("matches_result");
        
        Alter.Table("matches_result")
            .AlterColumn("filename").AsString().NotNullable();
    }
}