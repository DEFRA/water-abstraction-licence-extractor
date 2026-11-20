using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(94)]
public class AddErrorColumnToMatchesResult : Migration
{
    public override void Up()
    {
        Alter.Table("matches_result")
            .AddColumn("error").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("error").FromTable("matches_result");
    }
}
