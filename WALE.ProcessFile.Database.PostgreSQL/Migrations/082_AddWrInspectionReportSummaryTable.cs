using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(82)]
public class AddWrInspectionReportSummaryTable : Migration
{
    private const string SummaryTable = "wr_inspection_report_summary";

    public override void Up()
    {
        Create.Table(SummaryTable)
            .WithColumn("wr_inspection_report_summary_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("file_id").AsGuid().NotNullable()
            .WithColumn("matches_result_id").AsInt32().NotNullable()
            // Four dates rather than one: the first two come from the document, the last two from
            // DMS and describe when it was filed, not when the inspection happened.
            .WithColumn("inspection_date").AsDate().Nullable()
            .WithColumn("form_date").AsDate().Nullable()
            .WithColumn("dms_document_date").AsDate().Nullable()
            .WithColumn("dms_upload_date").AsDate().Nullable()
            // What a list orders on, and which of the four it came from.
            .WithColumn("sort_date").AsDate().Nullable()
            .WithColumn("date_source").AsString().NotNullable()
            // Set when the extracted date cannot be true, which completeness alone does not catch.
            .WithColumn("date_warning").AsString().Nullable()
            .WithColumn("permit_number").AsString().Nullable()
            .WithColumn("template").AsString().Nullable()
            .WithColumn("completeness").AsInt32().NotNullable()
            .WithColumn("is_scan").AsBoolean().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        // One row per file per run, so a rebuild cannot leave duplicates behind.
        Create.UniqueConstraint("uq_wr_inspection_report_summary_run_file")
            .OnTable(SummaryTable)
            .Columns("process_run_id", "file_id");

        Create.Index("ix_wr_inspection_report_summary_sort_date")
            .OnTable(SummaryTable)
            .OnColumn("sort_date").Ascending();

        // Serves the latest-run-per-file lookup the list view reads through.
        Create.Index("ix_wr_inspection_report_summary_file_run")
            .OnTable(SummaryTable)
            .OnColumn("file_id").Ascending()
            .OnColumn("process_run_id").Descending();
    }

    public override void Down()
    {
        Delete.Table(SummaryTable);
    }
}
