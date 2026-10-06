using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(80)]
public class AddIssueDateFlagColumnsToLicenceListItem : Migration
{
    private const string LicenceListItemTable = "licence_list_item";

    public override void Up()
    {
        Alter.Table(LicenceListItemTable)
            .AddColumn("nald_orig_signature_date")
            .AsDate()
            .Nullable()
            .AddColumn("nald_signature_date")
            .AsDate()
            .Nullable()
            .AddColumn("is_issue_date_flagged")
            .AsBoolean()
            .NotNullable()
            .WithDefaultValue(false);
    }

    public override void Down()
    {
        Delete.Column("is_issue_date_flagged")
            .FromTable(LicenceListItemTable);

        Delete.Column("nald_signature_date")
            .FromTable(LicenceListItemTable);

        Delete.Column("nald_orig_signature_date")
            .FromTable(LicenceListItemTable);
    }
}
