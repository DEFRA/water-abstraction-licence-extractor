using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(49)]
public class AddMatchesResultStatusField : Migration
{
    public override void Up()
    {
        Alter.Table("matches_result")
            .AddColumn("status").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("status").FromTable("matches_result");
    }
}