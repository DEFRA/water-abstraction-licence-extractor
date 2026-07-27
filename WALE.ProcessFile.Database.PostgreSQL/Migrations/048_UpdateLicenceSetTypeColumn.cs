using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

using FluentMigrator;

[Migration(48)]
public sealed class UpdateLicenceSetTypeColumn : Migration
{
    private const string LicenceSetTable =
        "licence_list_item_licence_set";

    private const string LicenceSetTypeTable =
        "licence_list_item_licence_set_type";

    private const string LicenceSetTypePrimaryKey =
        "pk_lic_list_set_type";

    public override void Up()
    {
        Execute.Sql(
            $"""
            ALTER TABLE {LicenceSetTable}
            ALTER COLUMN licence_set_type
            TYPE text
            USING licence_set_type::text;
            """);

        Delete.PrimaryKey(LicenceSetTypePrimaryKey)
            .FromTable(LicenceSetTypeTable);

        Execute.Sql(
            $"""
            ALTER TABLE {LicenceSetTypeTable}
            ALTER COLUMN licence_set_type
            TYPE text
            USING licence_set_type::text;

            ALTER TABLE {LicenceSetTypeTable}
            ALTER COLUMN licence_set_type
            SET NOT NULL;
            """);

        Create.PrimaryKey(LicenceSetTypePrimaryKey)
            .OnTable(LicenceSetTypeTable)
            .Columns(
                "licence_list_item_licence_set_id",
                "licence_set_type");
    }

    public override void Down()
    {
        Delete.PrimaryKey(LicenceSetTypePrimaryKey)
            .FromTable(LicenceSetTypeTable);

        Execute.Sql(
            $"""
            ALTER TABLE {LicenceSetTypeTable}
            ALTER COLUMN licence_set_type
            TYPE integer
            USING licence_set_type::integer;

            ALTER TABLE {LicenceSetTypeTable}
            ALTER COLUMN licence_set_type
            SET NOT NULL;
            """);

        Create.PrimaryKey(LicenceSetTypePrimaryKey)
            .OnTable(LicenceSetTypeTable)
            .Columns(
                "licence_list_item_licence_set_id",
                "licence_set_type");

        Execute.Sql(
            $"""
            ALTER TABLE {LicenceSetTable}
            ALTER COLUMN licence_set_type
            TYPE integer
            USING licence_set_type::integer;
            """);
    }
}