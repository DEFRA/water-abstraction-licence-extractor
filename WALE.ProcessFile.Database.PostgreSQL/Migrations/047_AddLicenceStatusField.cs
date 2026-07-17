using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(47)]
public class AddLicenceStatusField : Migration
{
    public override void Up()
    {
        Alter.Table("licence")
            .AddColumn("status").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("status").FromTable("licence");
    }
}