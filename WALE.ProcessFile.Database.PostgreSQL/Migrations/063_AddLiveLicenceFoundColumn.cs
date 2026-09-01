using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(63)]
public class AddLiveLicenceFoundColumn : Migration
{
    public override void Up()
    {
        Alter.Table("licence_finder_result")
            .AddColumn("live_licence_found").AsBoolean().Nullable();
    }

    public override void Down()
    {
        Delete.Column("live_licence_found").FromTable("licence_finder_result");
    }
}