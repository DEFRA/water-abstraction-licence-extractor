using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(26)]
public class AddLicenceFinderResultTable : Migration
{
    public override void Up()
    {
        Create.Table("licence_finder_result").InSchema("public")
            .WithColumn("permit_number").AsString().Nullable()
            .WithColumn("file_url").AsString().Nullable()
            .WithColumn("rule_used").AsString().Nullable()
            .WithColumn("change_audit_action").AsString().Nullable()
            .WithColumn("license_number").AsString().Nullable()
            .WithColumn("document_date").AsString().Nullable()
            .WithColumn("signature_date").AsString().Nullable()
            .WithColumn("date_of_issue").AsString().Nullable()
            .WithColumn("other_reference").AsString().Nullable()
            .WithColumn("file_size").AsString().Nullable()
            .WithColumn("disclosure_status").AsString().Nullable()
            .WithColumn("region").AsString().Nullable()
            .WithColumn("nald_id").AsInt32().Nullable()
            .WithColumn("nald_issue_no").AsInt32().Nullable()
            .WithColumn("nald_increment_no").AsInt32().Nullable()
            .WithColumn("primary_template").AsString().Nullable()
            .WithColumn("secondary_template").AsString().Nullable()
            .WithColumn("number_of_pages").AsInt32().Nullable()
            .WithColumn("doi_signature_date_match").AsBoolean().Nullable()
            .WithColumn("included_in_version_match").AsBoolean().Nullable()
            .WithColumn("single_licence_in_version_match").AsBoolean().Nullable()
            .WithColumn("version_match_file_url").AsString().Nullable()
            .WithColumn("duplicate_licence_in_version_match_result").AsBoolean().Nullable()
            .WithColumn("nald_issue").AsBoolean().Nullable()
            .WithColumn("file_id").AsString().Nullable()
            .WithColumn("file_id_status").AsString().Nullable()
            .WithColumn("file_id_status_change_date").AsString().Nullable()
            .WithColumn("is_water_company").AsBoolean().Nullable()
            .WithColumn("folder_name_auto_correct").AsBoolean().Nullable()
            .WithColumn("seen_in_dms_extract").AsBoolean().Nullable()
            .WithColumn("we_have_downloaded").AsBoolean().Nullable();
    }

    public override void Down()
    {
        Delete.Table("licence_finder_result");
    }
}