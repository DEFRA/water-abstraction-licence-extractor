using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

// A ProcessRun covers exactly one document type, not a mixed batch of AbstractionLicence and
// WrInspectionReport files - per-document-type success/failure/duration stats become a
// straight query against process_run instead of a join through every file in a mixed batch,
// and it matches what ProcessRun already implicitly means ("this batch, this document type").
// A caller discovering files for multiple document types runs its dispatch cycle once per
// type, each with its own ProcessRun, rather than tagging individual files within one shared
// run.
[Migration(76)]
public class AddDocumentTypeColumnToProcessRun : Migration
{
    public override void Up()
    {
        Alter.Table("process_run")
            .AddColumn("document_type").AsString().NotNullable().WithDefaultValue("AbstractionLicence");
    }

    public override void Down()
    {
        Delete.Column("document_type").FromTable("process_run");
    }
}
