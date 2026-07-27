using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(52)]
public class AddColumnToListLinkLicencesTable : Migration
{
   private const string TableName =
        "licence_list_item_linked_licence";

    public override void Up()
    {
        Alter.Table(TableName)
            .AddColumn("nald_revocation_date")
                .AsCustom("timestamp without time zone")
                .Nullable()
            .AddColumn("nald_expiry_date")
                .AsCustom("timestamp without time zone")
                .Nullable()
            .AddColumn("nald_orig_effective_date")
                .AsCustom("timestamp without time zone")
                .Nullable()
            .AddColumn("nald_orig_signature_date")
                .AsCustom("timestamp without time zone")
                .Nullable()
            .AddColumn("nald_signature_date")
                .AsCustom("timestamp without time zone")
                .Nullable()
            .AddColumn("nald_effective_start_date")
                .AsCustom("timestamp without time zone")
                .Nullable()
            .AddColumn("nald_effective_end_date")
                .AsCustom("timestamp without time zone")
                .Nullable()
            .AddColumn("nald_issue_number")
                .AsInt32()
                .Nullable()
            .AddColumn("nald_increment_number")
                .AsInt32()
                .Nullable()
            .AddColumn("nald_update_reason")
                .AsString(50)
                .Nullable();
    }

    public override void Down()
    {
        Delete.Column("nald_revocation_date")
            .FromTable(TableName);

        Delete.Column("nald_expiry_date")
            .FromTable(TableName);

        Delete.Column("nald_orig_effective_date")
            .FromTable(TableName);

        Delete.Column("nald_orig_signature_date")
            .FromTable(TableName);

        Delete.Column("nald_signature_date")
            .FromTable(TableName);

        Delete.Column("nald_effective_start_date")
            .FromTable(TableName);

        Delete.Column("nald_effective_end_date")
            .FromTable(TableName);

        Delete.Column("nald_issue_number")
            .FromTable(TableName);

        Delete.Column("nald_increment_number")
            .FromTable(TableName);

        Delete.Column("nald_update_reason")
            .FromTable(TableName);
    }
}