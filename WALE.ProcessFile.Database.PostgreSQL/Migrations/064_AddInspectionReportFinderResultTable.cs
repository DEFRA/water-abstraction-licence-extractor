using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

// Analogous to licence_finder_result (026_AddLicenceFinderResultTable), but for inspection
// reports (WR51) rather than abstraction licences - deliberately much leaner, since NALD holds
// no inspection-report data at all (confirmed empirically 2026-09-09, see the
// wr51_portal_bff_analysis memory) and so there is no NALD-matching concept here: no nald_id,
// no doi_signature_date_match, no live_licence_found, etc. This table is purely the output of
// LicenceFileFinder.FindInspectionReportFiles's filename/folder filter over dms_extract - the
// entire discovery mechanism for this document type, not one signal among several.
[Migration(64)]
public class AddInspectionReportFinderResultTable : Migration
{
    public override void Up()
    {
        Create.Table("inspection_report_finder_result").InSchema("public")
            .WithColumn("permit_number").AsString().Nullable()
            .WithColumn("file_url").AsString().Nullable()
            .WithColumn("file_name").AsString().Nullable()
            .WithColumn("library_name").AsString().Nullable()
            .WithColumn("regime").AsString().Nullable()
            .WithColumn("file_size").AsString().Nullable()
            .WithColumn("file_id").AsString().Nullable()
            .WithColumn("document_date").AsString().Nullable()
            .WithColumn("other_reference").AsString().Nullable()
            .WithColumn("disclosure_status").AsString().Nullable()
            .WithColumn("process_run_id").AsInt32().Nullable();
    }

    public override void Down()
    {
        Delete.Table("inspection_report_finder_result");
    }
}
