using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(86)]
public sealed class CreateDocumentText : Migration
{
    private const string DocumentTextTable = "document_text";

    public override void Up()
    {
        Create.Table(DocumentTextTable)
            .WithColumn("document_text_id")
            .AsInt64()
            .NotNullable()
            .PrimaryKey()
            .Identity()

            .WithColumn("file_id")
            .AsGuid()
            .NotNullable()

            .WithColumn("source_service")
            .AsString()
            .NotNullable()

            .WithColumn("format_version")
            .AsInt32()
            .NotNullable()

            .WithColumn("text")
            .AsString(int.MaxValue)
            .NotNullable()

            .WithColumn("date_time_utc")
            .AsDateTime()
            .NotNullable();

        Create.UniqueConstraint("uq_document_text_file_id")
            .OnTable(DocumentTextTable)
            .Column("file_id");
    }

    public override void Down()
    {
        Delete.Table(DocumentTextTable);
    }
}
