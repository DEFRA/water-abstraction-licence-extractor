using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(60)]
public class AddCurrentVerificationTypeField : Migration
{
    private const string LicenceListVerificationItem = "licence_list_item_verification_item";
    private const string CurrentVerificationType = "current_verification_type";
    public override void Up()
    {
        Alter.Table(LicenceListVerificationItem)
            .AddColumn(CurrentVerificationType).AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column(CurrentVerificationType).FromTable(LicenceListVerificationItem);
    }
}