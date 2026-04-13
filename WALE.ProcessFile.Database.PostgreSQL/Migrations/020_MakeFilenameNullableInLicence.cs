using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(20)]
public class MakeFilenameNullableInLicence : Migration
{
    public override void Up()
    {
        Alter.Table("licence")
            .AlterColumn("filename").AsString().Nullable();
    }

    public override void Down()
    {
        Alter.Table("licence")
            .AlterColumn("filename").AsString().NotNullable();
    }
}