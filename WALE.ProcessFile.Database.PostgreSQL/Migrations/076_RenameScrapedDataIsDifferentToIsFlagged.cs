using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(76)]
public class RenameScrapedDataIsDifferentToIsFlagged : Migration
{
    private const string VerificationItemTable = "licence_list_item_verification_item";

    public override void Up()
    {
        Rename.Column("scraped_data_is_different")
            .OnTable(VerificationItemTable)
            .To("is_flagged");

        Alter.Table(VerificationItemTable)
            .AddColumn("flag_reason")
            .AsCustom("text")
            .Nullable();
    }

    public override void Down()
    {
        Delete.Column("flag_reason")
            .FromTable(VerificationItemTable);

        Rename.Column("is_flagged")
            .OnTable(VerificationItemTable)
            .To("scraped_data_is_different");
    }
}
