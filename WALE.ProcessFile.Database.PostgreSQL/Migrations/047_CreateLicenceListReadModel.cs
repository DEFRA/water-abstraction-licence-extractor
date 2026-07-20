using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(202607201300)]
public sealed class CreateLicenceListReadModel : Migration
{
    private const string LicenceListItemTable =
        "licence_list_item";

    private const string PurposeTable =
        "licence_list_item_purpose";

    private const string PointTable =
        "licence_list_item_point";

    private const string LinkedLicenceTable =
        "licence_list_item_linked_licence";

    private const string LinkLocationTable =
        "licence_list_item_link_location";

    private const string LicenceSetTable =
        "licence_list_item_licence_set";

    private const string LicenceSetTypeTable =
        "licence_list_item_licence_set_type";

    private const string VerificationSectionTable =
        "licence_list_item_verification_section";

    private const string VerificationItemTable =
        "licence_list_item_verification_item";

    private const string VerificationTypeTable =
        "licence_list_item_verification_type";

    public override void Up()
    {
        CreatePostgresExtensions();

        CreateLicenceListItemTable();
        CreatePurposeTable();
        CreatePointTable();

        CreateLinkedLicenceTable();
        CreateLinkLocationTable();

        CreateLicenceSetTable();
        CreateLicenceSetTypeTable();

        CreateVerificationSectionTable();
        CreateVerificationItemTable();
        CreateVerificationTypeTable();

        CreateLicenceListItemIndexes();
        CreatePurposeIndexes();
        CreatePointIndexes();

        CreateLinkedLicenceIndexes();
        CreateLinkLocationIndexes();

        CreateLicenceSetIndexes();
        CreateVerificationIndexes();

        CreateTrigramIndexes();
    }

    public override void Down()
    {
        // Child-to-parent order because of foreign-key constraints.
        Delete.Table(VerificationTypeTable);
        Delete.Table(VerificationItemTable);
        Delete.Table(VerificationSectionTable);

        Delete.Table(LicenceSetTypeTable);
        Delete.Table(LicenceSetTable);

        Delete.Table(LinkLocationTable);
        Delete.Table(LinkedLicenceTable);

        Delete.Table(PointTable);
        Delete.Table(PurposeTable);

        Delete.Table(LicenceListItemTable);

        // Do not remove pg_trgm because another migration/table may use it.
    }

    private void CreatePostgresExtensions()
    {
        Execute.Sql(
            """
            CREATE EXTENSION IF NOT EXISTS pg_trgm;
            """);
    }

    private void CreateLicenceListItemTable()
    {
        Create.Table(LicenceListItemTable)
            .WithColumn("licence_list_item_id")
                .AsInt64()
                .PrimaryKey()
                .Identity()

            .WithColumn("process_run_id")
                .AsInt32()
                .NotNullable()

            .WithColumn("file_id")
                .AsGuid()
                .NotNullable()

            .WithColumn("filename")
                .AsCustom("text")
                .NotNullable()

            .WithColumn("licence_number")
                .AsCustom("text")
                .Nullable()

            .WithColumn("licence_holder")
                .AsCustom("text")
                .Nullable()

            .WithColumn("limits_count")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(0)

            .WithColumn("aggregates_count")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(0)

            .WithColumn("ocr")
                .AsBoolean()
                .NotNullable()
                .WithDefaultValue(false)

            .WithColumn("issue_date")
                .AsDate()
                .Nullable()

            .WithColumn("issue_year")
                .AsInt16()
                .Nullable()

            .WithColumn("issuer")
                .AsCustom("text")
                .Nullable()

            .WithColumn("means_found")
                .AsBoolean()
                .NotNullable()
                .WithDefaultValue(false)

            .WithColumn("status")
                .AsCustom("text")
                .Nullable()

            // Empty/not-empty filters.
            .WithColumn("purposes_count")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(0)

            .WithColumn("points_count")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(0)

            .WithColumn("linked_licences_count")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(0)

            .WithColumn("licence_sets_count")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(0)

            // Verification summary fields used by the list filters.
            .WithColumn("verification_sections_count")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(0)

            .WithColumn("verification_items_count")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(0)

            .WithColumn("has_verifications")
                .AsBoolean()
                .NotNullable()
                .WithDefaultValue(false)

            .WithColumn("created_date_time_utc")
                .AsCustom("timestamp with time zone")
                .NotNullable()

            .WithColumn("updated_date_time_utc")
                .AsCustom("timestamp with time zone")
                .NotNullable();

        Execute.Sql(
            """
            ALTER TABLE licence_list_item
                ALTER COLUMN created_date_time_utc SET DEFAULT now(),
                ALTER COLUMN updated_date_time_utc SET DEFAULT now();
            """);

        Create.UniqueConstraint("uq_lic_list_process_run_file")
            .OnTable(LicenceListItemTable)
            .Columns(
                "process_run_id",
                "file_id");

        /*
         * Required so child tables can use a composite foreign key and
         * guarantee that process_run_id matches the parent item.
         */
        Create.UniqueConstraint("uq_lic_list_run_item")
            .OnTable(LicenceListItemTable)
            .Columns(
                "process_run_id",
                "licence_list_item_id");
    }

    private void CreatePurposeTable()
    {
        Create.Table(PurposeTable)
            .WithColumn("licence_list_item_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("purpose")
                .AsCustom("text")
                .NotNullable()

            .WithColumn("sort_order")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(0);

        Create.PrimaryKey("pk_lic_list_purpose")
            .OnTable(PurposeTable)
            .Columns(
                "licence_list_item_id",
                "purpose");

        Create.ForeignKey("fk_lic_list_purpose_item")
            .FromTable(PurposeTable)
            .ForeignColumn("licence_list_item_id")
            .ToTable(LicenceListItemTable)
            .PrimaryColumn("licence_list_item_id")
            .OnDelete(Rule.Cascade);
    }

    private void CreatePointTable()
    {
        Create.Table(PointTable)
            .WithColumn("licence_list_item_point_id")
                .AsInt64()
                .PrimaryKey()
                .Identity()

            .WithColumn("licence_list_item_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("point")
                .AsCustom("text")
                .NotNullable()

            .WithColumn("sort_order")
                .AsInt32()
                .NotNullable()
                .WithDefaultValue(0);

        Create.ForeignKey("fk_lic_list_point_item")
            .FromTable(PointTable)
            .ForeignColumn("licence_list_item_id")
            .ToTable(LicenceListItemTable)
            .PrimaryColumn("licence_list_item_id")
            .OnDelete(Rule.Cascade);
    }

    private void CreateLinkedLicenceTable()
    {
        Create.Table(LinkedLicenceTable)
            .WithColumn("linked_licence_id")
                .AsInt64()
                .PrimaryKey()
                .Identity()

            .WithColumn("licence_list_item_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("licence_number")
                .AsCustom("text")
                .Nullable()

            .WithColumn("raw_scraped_licence_number")
                .AsCustom("text")
                .Nullable()

            .WithColumn("dms_permit_number")
                .AsCustom("text")
                .Nullable()

            .WithColumn("dms_file_id")
                .AsGuid()
                .Nullable()

            .WithColumn("filename")
                .AsCustom("text")
                .Nullable()

            .WithColumn("dms_path")
                .AsCustom("text")
                .Nullable()

            .WithColumn("licence_version_id")
                .AsCustom("text")
                .Nullable()

            .WithColumn("effective_date")
                .AsDate()
                .Nullable()

            .WithColumn("issue_date")
                .AsDate()
                .Nullable()

            .WithColumn("issuer")
                .AsCustom("text")
                .Nullable()

            .WithColumn("nald_status")
                .AsCustom("text")
                .Nullable()

            .WithColumn("licence_type")
                .AsCustom("text")
                .Nullable()

            .WithColumn("region_id")
                .AsInt32()
                .Nullable()

            .WithColumn("source_data")
                .AsCustom("jsonb")
                .Nullable();

        Create.ForeignKey("fk_lic_list_linked_item")
            .FromTable(LinkedLicenceTable)
            .ForeignColumn("licence_list_item_id")
            .ToTable(LicenceListItemTable)
            .PrimaryColumn("licence_list_item_id")
            .OnDelete(Rule.Cascade);
    }

    private void CreateLinkLocationTable()
    {
        Create.Table(LinkLocationTable)
            .WithColumn("link_location_id")
                .AsInt64()
                .PrimaryKey()
                .Identity()

            .WithColumn("linked_licence_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("source")
                .AsCustom("text")
                .Nullable()

            .WithColumn("direction")
                .AsCustom("text")
                .Nullable()

            .WithColumn("section_name")
                .AsCustom("text")
                .Nullable()

            .WithColumn("link_reason")
                .AsCustom("text")
                .Nullable()

            .WithColumn("is_because_of_aggregate")
                .AsBoolean()
                .Nullable()

            .WithColumn("line_number")
                .AsInt32()
                .Nullable()

            .WithColumn("page_number")
                .AsInt32()
                .Nullable();

        Create.ForeignKey("fk_lic_list_location_link")
            .FromTable(LinkLocationTable)
            .ForeignColumn("linked_licence_id")
            .ToTable(LinkedLicenceTable)
            .PrimaryColumn("linked_licence_id")
            .OnDelete(Rule.Cascade);
    }

    private void CreateLicenceSetTable()
    {
        Create.Table(LicenceSetTable)
            .WithColumn("licence_list_item_licence_set_id")
                .AsInt64()
                .PrimaryKey()
                .Identity()

            /*
             * Deliberately duplicated from the parent to support:
             *
             * SELECT DISTINCT short_licence_set_id
             * WHERE process_run_id = @ProcessRunId
             */
            .WithColumn("process_run_id")
                .AsInt32()
                .NotNullable()

            .WithColumn("licence_list_item_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("licence_set_id")
                .AsCustom("text")
                .NotNullable()

            .WithColumn("short_licence_set_id")
                .AsCustom("text")
                .Nullable()

            .WithColumn("licence_set_type")
                .AsInt32()
                .Nullable();

        Create.ForeignKey("fk_lic_list_set_run_item")
            .FromTable(LicenceSetTable)
            .ForeignColumns(
                "process_run_id",
                "licence_list_item_id")
            .ToTable(LicenceListItemTable)
            .PrimaryColumns(
                "process_run_id",
                "licence_list_item_id")
            .OnDelete(Rule.Cascade);

        Create.UniqueConstraint("uq_lic_list_item_set")
            .OnTable(LicenceSetTable)
            .Columns(
                "licence_list_item_id",
                "licence_set_id");
    }

    private void CreateLicenceSetTypeTable()
    {
        Create.Table(LicenceSetTypeTable)
            .WithColumn("licence_list_item_licence_set_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("licence_set_type")
                .AsInt32()
                .NotNullable();

        Create.PrimaryKey("pk_lic_list_set_type")
            .OnTable(LicenceSetTypeTable)
            .Columns(
                "licence_list_item_licence_set_id",
                "licence_set_type");

        Create.ForeignKey("fk_lic_list_set_type_set")
            .FromTable(LicenceSetTypeTable)
            .ForeignColumn("licence_list_item_licence_set_id")
            .ToTable(LicenceSetTable)
            .PrimaryColumn("licence_list_item_licence_set_id")
            .OnDelete(Rule.Cascade);
    }

    private void CreateVerificationSectionTable()
    {
        Create.Table(VerificationSectionTable)
            .WithColumn("verification_section_id")
                .AsInt64()
                .PrimaryKey()
                .Identity()

            /*
             * Also deliberately duplicated for process-run-wide
             * verification filter options.
             */
            .WithColumn("process_run_id")
                .AsInt32()
                .NotNullable()

            .WithColumn("licence_list_item_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("licence_section_name")
                .AsCustom("text")
                .NotNullable();

        Create.ForeignKey("fk_lic_list_ver_section_run_item")
            .FromTable(VerificationSectionTable)
            .ForeignColumns(
                "process_run_id",
                "licence_list_item_id")
            .ToTable(LicenceListItemTable)
            .PrimaryColumns(
                "process_run_id",
                "licence_list_item_id")
            .OnDelete(Rule.Cascade);

        Create.UniqueConstraint("uq_lic_list_ver_section")
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
                .PrimaryKey()
                .Identity()

            .WithColumn("verification_section_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("licence_section_item_id")
                .AsCustom("text")
                .NotNullable();

        Create.ForeignKey("fk_lic_list_ver_item_section")
            .FromTable(VerificationItemTable)
            .ForeignColumn("verification_section_id")
            .ToTable(VerificationSectionTable)
            .PrimaryColumn("verification_section_id")
            .OnDelete(Rule.Cascade);

        Create.UniqueConstraint("uq_lic_list_ver_item")
            .OnTable(VerificationItemTable)
            .Columns(
                "verification_section_id",
                "licence_section_item_id");
    }

    private void CreateVerificationTypeTable()
    {
        Create.Table(VerificationTypeTable)
            .WithColumn("verification_item_id")
                .AsInt64()
                .NotNullable()

            .WithColumn("verification_type")
                .AsCustom("text")
                .NotNullable();

        Create.PrimaryKey("pk_lic_list_ver_type")
            .OnTable(VerificationTypeTable)
            .Columns(
                "verification_item_id",
                "verification_type");

        Create.ForeignKey("fk_lic_list_ver_type_item")
            .FromTable(VerificationTypeTable)
            .ForeignColumn("verification_item_id")
            .ToTable(VerificationItemTable)
            .PrimaryColumn("verification_item_id")
            .OnDelete(Rule.Cascade);
    }

    private void CreateLicenceListItemIndexes()
    {
        /*
         * This index supports the default process-run paging query:
         *
         * WHERE process_run_id = @ProcessRunId
         * ORDER BY licence_list_item_id
         */
        Create.Index("ix_lic_list_run_item")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_filename")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("filename")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_number")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("licence_number")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_holder")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("licence_holder")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_issue_date")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("issue_date")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_issue_year")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("issue_year")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        /*
         * Used to get every issuer for a process run independently
         * of the current page size.
         */
        Create.Index("ix_lic_list_run_issuer")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("issuer")
                .Ascending();

        Create.Index("ix_lic_list_run_status")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("status")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_ocr")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("ocr")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_means")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("means_found")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_limits")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("limits_count")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_aggregates")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("aggregates_count")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_purposes_count")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("purposes_count")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_points_count")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("points_count")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_linked_count")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("linked_licences_count")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_sets_count")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("licence_sets_count")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_run_has_ver")
            .OnTable(LicenceListItemTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("has_verifications")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();
    }

    private void CreatePurposeIndexes()
    {
        Create.Index("ix_lic_list_purpose_order")
            .OnTable(PurposeTable)
            .OnColumn("licence_list_item_id")
                .Ascending()
            .OnColumn("sort_order")
                .Ascending();
    }

    private void CreatePointIndexes()
    {
        Create.Index("ix_lic_list_point_order")
            .OnTable(PointTable)
            .OnColumn("licence_list_item_id")
                .Ascending()
            .OnColumn("sort_order")
                .Ascending();
    }

    private void CreateLinkedLicenceIndexes()
    {
        Create.Index("ix_lic_list_linked_item")
            .OnTable(LinkedLicenceTable)
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_linked_number")
            .OnTable(LinkedLicenceTable)
            .OnColumn("licence_number")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_linked_dms_permit")
            .OnTable(LinkedLicenceTable)
            .OnColumn("dms_permit_number")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_list_linked_type")
            .OnTable(LinkedLicenceTable)
            .OnColumn("licence_type")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();
    }

    private void CreateLinkLocationIndexes()
    {
        Create.Index("ix_lic_list_location_link")
            .OnTable(LinkLocationTable)
            .OnColumn("linked_licence_id")
                .Ascending();

        Create.Index("ix_lic_list_location_direction")
            .OnTable(LinkLocationTable)
            .OnColumn("direction")
                .Ascending()
            .OnColumn("linked_licence_id")
                .Ascending();

        Create.Index("ix_lic_list_location_reason")
            .OnTable(LinkLocationTable)
            .OnColumn("link_reason")
                .Ascending()
            .OnColumn("linked_licence_id")
                .Ascending();

        Create.Index("ix_lic_list_location_section")
            .OnTable(LinkLocationTable)
            .OnColumn("section_name")
                .Ascending()
            .OnColumn("linked_licence_id")
                .Ascending();
    }

    private void CreateLicenceSetIndexes()
    {
        /*
         * Supports dynamic filter options across the whole process run:
         *
         * SELECT DISTINCT short_licence_set_id
         * FROM licence_list_item_licence_set
         * WHERE process_run_id = @ProcessRunId
         */
        Create.Index("ix_lic_set_run_short_id")
            .OnTable(LicenceSetTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("short_licence_set_id")
                .Ascending();

        Create.Index("ix_lic_set_run_full_id")
            .OnTable(LicenceSetTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("licence_set_id")
                .Ascending();

        /*
         * Supports the list filter EXISTS query.
         */
        Create.Index("ix_lic_set_item_short_id")
            .OnTable(LicenceSetTable)
            .OnColumn("licence_list_item_id")
                .Ascending()
            .OnColumn("short_licence_set_id")
                .Ascending();

        Create.Index("ix_lic_set_item_full_id")
            .OnTable(LicenceSetTable)
            .OnColumn("licence_list_item_id")
                .Ascending()
            .OnColumn("licence_set_id")
                .Ascending();

        Create.Index("ix_lic_set_run_type")
            .OnTable(LicenceSetTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("licence_set_type")
                .Ascending()
            .OnColumn("licence_list_item_id")
                .Ascending();

        Create.Index("ix_lic_set_type_value")
            .OnTable(LicenceSetTypeTable)
            .OnColumn("licence_set_type")
                .Ascending()
            .OnColumn("licence_list_item_licence_set_id")
                .Ascending();
    }

    private void CreateVerificationIndexes()
    {
        /*
         * Supports process-run-wide verification section options.
         */
        Create.Index("ix_lic_ver_section_run_name")
            .OnTable(VerificationSectionTable)
            .OnColumn("process_run_id")
                .Ascending()
            .OnColumn("licence_section_name")
                .Ascending();

        /*
         * Supports EXISTS filters starting from a licence list item.
         */
        Create.Index("ix_lic_ver_section_item_name")
            .OnTable(VerificationSectionTable)
            .OnColumn("licence_list_item_id")
                .Ascending()
            .OnColumn("licence_section_name")
                .Ascending();

        Create.Index("ix_lic_ver_item_section_item")
            .OnTable(VerificationItemTable)
            .OnColumn("verification_section_id")
                .Ascending()
            .OnColumn("licence_section_item_id")
                .Ascending();

        Create.Index("ix_lic_ver_type_value")
            .OnTable(VerificationTypeTable)
            .OnColumn("verification_type")
                .Ascending()
            .OnColumn("verification_item_id")
                .Ascending();
    }

    private void CreateTrigramIndexes()
    {
        /*
         * Used by searches such as:
         *
         * column ILIKE '%' || @SearchTerm || '%'
         */

        Execute.Sql(
            """
            CREATE INDEX ix_lic_list_filename_trgm
                ON licence_list_item
                USING gin (filename gin_trgm_ops);
            """);

        Execute.Sql(
            """
            CREATE INDEX ix_lic_list_number_trgm
                ON licence_list_item
                USING gin (licence_number gin_trgm_ops);
            """);

        Execute.Sql(
            """
            CREATE INDEX ix_lic_list_holder_trgm
                ON licence_list_item
                USING gin (licence_holder gin_trgm_ops);
            """);

        Execute.Sql(
            """
            CREATE INDEX ix_lic_list_search_text_trgm
                ON licence_list_item
                USING gin (search_text gin_trgm_ops);
            """);

        Execute.Sql(
            """
            CREATE INDEX ix_lic_list_linked_number_trgm
                ON licence_list_item_linked_licence
                USING gin (licence_number gin_trgm_ops);
            """);

        Execute.Sql(
            """
            CREATE INDEX ix_lic_list_linked_raw_number_trgm
                ON licence_list_item_linked_licence
                USING gin (raw_scraped_licence_number gin_trgm_ops);
            """);
    }
}