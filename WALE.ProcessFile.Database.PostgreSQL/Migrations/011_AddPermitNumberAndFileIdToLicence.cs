using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(11)]
public class AddPermitNumberAndFileIdToLicence : Migration
{
    public override void Up()
    {
        Alter.Table("licence")
            .AddColumn("permit_number").AsString().Nullable()
            .AddColumn("file_id").AsGuid().Nullable();
    }

    public override void Down()
    {
        Delete.Column("permit_number").FromTable("licence");
        Delete.Column("file_id").FromTable("licence");
    }
}