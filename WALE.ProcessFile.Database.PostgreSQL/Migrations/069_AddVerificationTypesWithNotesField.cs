using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(69)]
public class AddVerificationTypesWithNotesField : Migration
{
    private const string LicenceListVerificationItem =
        "licence_list_item_verification_item";

    private const string VerificationTypesWithNotes =
        "verification_types_with_notes";

    public override void Up()
    {
        if (Schema.Table(LicenceListVerificationItem)
            .Column(VerificationTypesWithNotes)
            .Exists())
        {
            Delete.Column(VerificationTypesWithNotes)
                .FromTable(LicenceListVerificationItem);
        }

        Alter.Table(LicenceListVerificationItem)
            .AddColumn(VerificationTypesWithNotes)
            .AsCustom("text[]")
            .Nullable();
    }

    public override void Down()
    {
        if (Schema.Table(LicenceListVerificationItem)
            .Column(VerificationTypesWithNotes)
            .Exists())
        {
            Delete.Column(VerificationTypesWithNotes)
                .FromTable(LicenceListVerificationItem);
        }
    }
}