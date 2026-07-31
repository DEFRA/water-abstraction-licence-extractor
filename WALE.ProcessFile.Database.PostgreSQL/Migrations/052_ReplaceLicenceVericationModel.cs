using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(52)]
public sealed class ReplaceLicenceVerificationModel : Migration
{
  private const string LicenceListItemTable =
        "licence_list_item";

    private const string VerificationSectionTable =
        "licence_list_item_verification_section";

    private const string VerificationItemTable =
        "licence_list_item_verification_item";

    private const string VerificationTypeTable =
        "licence_list_item_verification_type";

    public override void Up()
    {
        DropExistingVerificationTables();
        
        CreateVerificationSectionTable();
        CreateVerificationItemTable();

        CreateVerificationIndexes();
    }

    public override void Down()
    {
        DropNewVerificationTables();

        RecreateOriginalVerificationSectionTable();
        RecreateOriginalVerificationItemTable();
        RecreateOriginalVerificationTypeTable();

        RecreateOriginalVerificationIndexes();
    }

    private void DropExistingVerificationTables()
    {
        Execute.Sql(
            """
            DROP TABLE IF EXISTS
                licence_list_item_verification_item_type
                CASCADE;

            DROP TABLE IF EXISTS
                licence_list_item_verification_type
                CASCADE;

            DROP TABLE IF EXISTS
                licence_list_item_verification_item
                CASCADE;

            DROP TABLE IF EXISTS
                licence_list_item_verification_section
                CASCADE;
            """);
    }

    private void CreateVerificationSectionTable()
    {
        Create.Table(VerificationSectionTable)
            .WithColumn("verification_section_id")
                .AsInt64()
                .PrimaryKey(
                    "pk_lic_list_ver_section")
                .Identity()

            .WithColumn("process_run_id")
                .AsInt32()
                .NotNullable()

            .WithColumn("licence_list_item_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("licence_section_name")
                .AsCustom("text")
                .NotNullable();

        Create.ForeignKey(
                "fk_lic_list_ver_section_run_item")
            .FromTable(VerificationSectionTable)
            .ForeignColumns(
                "process_run_id",
                "licence_list_item_id")
            .ToTable(LicenceListItemTable)
            .PrimaryColumns(
                "process_run_id",
                "licence_list_item_id")
            .OnDelete(Rule.Cascade);

        Create.UniqueConstraint(
                "uq_lic_list_ver_section")
            .OnTable(VerificationSectionTable)
            .Columns(
                "licence_list_item_id",
                "licence_section_name");
    }

    private void CreateVerificationItemTable()
    {
        Create.Table(VerificationItemTable)
            .WithColumn("verification_item_id")
                .AsInt64()
                .PrimaryKey(
                    "pk_lic_list_ver_item")
                .Identity()

            .WithColumn("verification_section_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("licence_section_item_id")
                .AsCustom("text")
                .NotNullable()

            .WithColumn("verification_types")
                .AsCustom("text[]")
                .NotNullable()
                .WithDefaultValue(
                    RawSql.Insert(
                        "ARRAY[]::text[]"))

            .WithColumn("scraped_data_is_different")
                .AsBoolean()
                .NotNullable()
                .WithDefaultValue(false);

        Create.ForeignKey(
                "fk_lic_list_ver_item_section")
            .FromTable(VerificationItemTable)
            .ForeignColumn(
                "verification_section_id")
            .ToTable(VerificationSectionTable)
            .PrimaryColumn(
                "verification_section_id")
            .OnDelete(Rule.Cascade);

        Create.UniqueConstraint(
                "uq_lic_list_ver_item")
            .OnTable(VerificationItemTable)
            .Columns(
                "verification_section_id",
                "licence_section_item_id");
    }

    private void CreateVerificationIndexes()
    {
        Create.Index(
                "ix_lic_ver_section_run_name")
            .OnTable(VerificationSectionTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("licence_section_name")
                .Ascending();

        Create.Index(
                "ix_lic_ver_section_item_name")
            .OnTable(VerificationSectionTable)
            .OnColumn("licence_list_item_id")
                .Ascending()
            .OnColumn("licence_section_name")
                .Ascending();

        Create.Index(
                "ix_lic_ver_item_section_item")
            .OnTable(VerificationItemTable)
            .OnColumn("verification_section_id")
                .Ascending()
            .OnColumn("licence_section_item_id")
                .Ascending();

        Execute.Sql(
            """
            CREATE INDEX ix_lic_ver_item_types
                ON licence_list_item_verification_item
                USING GIN (verification_types);
            """);
    }

    private void DropNewVerificationTables()
    {
        Execute.Sql(
            """
            DROP TABLE IF EXISTS
                licence_list_item_verification_item
                CASCADE;

            DROP TABLE IF EXISTS
                licence_list_item_verification_section
                CASCADE;
            """);
    }

    private void RecreateOriginalVerificationSectionTable()
    {
        Create.Table(VerificationSectionTable)
            .WithColumn("verification_section_id")
                .AsInt64()
                .PrimaryKey()
                .Identity()

            .WithColumn("process_run_id")
                .AsInt32()
                .NotNullable()

            .WithColumn("licence_list_item_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("licence_section_name")
                .AsCustom("text")
                .NotNullable();

        Create.ForeignKey(
                "fk_lic_list_ver_section_run_item")
            .FromTable(VerificationSectionTable)
            .ForeignColumns(
                "process_run_id",
                "licence_list_item_id")
            .ToTable(LicenceListItemTable)
            .PrimaryColumns(
                "process_run_id",
                "licence_list_item_id")
            .OnDelete(Rule.Cascade);

        Create.UniqueConstraint(
                "uq_lic_list_ver_section")
            .OnTable(VerificationSectionTable)
            .Columns(
                "licence_list_item_id",
                "licence_section_name");
    }

    private void RecreateOriginalVerificationItemTable()
    {
        Create.Table(VerificationItemTable)
            .WithColumn("verification_item_id")
                .AsInt64()
                .PrimaryKey()
                .Identity()

            .WithColumn("verification_section_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("licence_section_item_id")
                .AsCustom("text")
                .NotNullable();

        Create.ForeignKey(
                "fk_lic_list_ver_item_section")
            .FromTable(VerificationItemTable)
            .ForeignColumn(
                "verification_section_id")
            .ToTable(VerificationSectionTable)
            .PrimaryColumn(
                "verification_section_id")
            .OnDelete(Rule.Cascade);

        Create.UniqueConstraint(
                "uq_lic_list_ver_item")
            .OnTable(VerificationItemTable)
            .Columns(
                "verification_section_id",
                "licence_section_item_id");
    }

    private void RecreateOriginalVerificationTypeTable()
    {
        Create.Table(VerificationTypeTable)
            .WithColumn("verification_item_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("verification_type")
                .AsCustom("text")
                .NotNullable();

        Create.PrimaryKey(
                "pk_lic_list_ver_type")
            .OnTable(VerificationTypeTable)
            .Columns(
                "verification_item_id",
                "verification_type");

        Create.ForeignKey(
                "fk_lic_list_ver_type_item")
            .FromTable(VerificationTypeTable)
            .ForeignColumn("verification_item_id")
            .ToTable(VerificationItemTable)
            .PrimaryColumn("verification_item_id")
            .OnDelete(Rule.Cascade);
    }

    private void RecreateOriginalVerificationIndexes()
    {
        Create.Index(
                "ix_lic_ver_section_run_name")
            .OnTable(VerificationSectionTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("licence_section_name")
                .Ascending();

        Create.Index(
                "ix_lic_ver_section_item_name")
            .OnTable(VerificationSectionTable)
            .OnColumn("licence_list_item_id")
                .Ascending()
            .OnColumn("licence_section_name")
                .Ascending();

        Create.Index(
                "ix_lic_ver_item_section_item")
            .OnTable(VerificationItemTable)
            .OnColumn("verification_section_id")
                .Ascending()
            .OnColumn("licence_section_item_id")
                .Ascending();

        Create.Index(
                "ix_lic_ver_type_value")
            .OnTable(VerificationTypeTable)
            .OnColumn("verification_type")
                .Ascending()
            .OnColumn("verification_item_id")
                .Ascending();
    }
}   