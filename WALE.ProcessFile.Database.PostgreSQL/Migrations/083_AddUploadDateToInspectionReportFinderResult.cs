using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

// DMS holds an upload date for every file, including those whose document_date is blank, so this
// is the only date available for some WR51 documents. String like document_date beside it: the
// DMS extract supplies both as free text and the parsing happens on read.
[Migration(83)]
public class AddUploadDateToInspectionReportFinderResult : Migration
{
    public override void Up()
    {
        Alter.Table("inspection_report_finder_result").InSchema("public")
            .AddColumn("upload_date").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("upload_date").FromTable("inspection_report_finder_result").InSchema("public");
    }
}
