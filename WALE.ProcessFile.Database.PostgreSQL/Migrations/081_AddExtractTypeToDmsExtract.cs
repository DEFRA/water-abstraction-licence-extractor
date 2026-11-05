using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

// One dms_extract holds an extract per document type, so importing one leaves the others in place.
[Migration(81)]
public class AddExtractTypeToDmsExtract : Migration
{
    private const string DmsExtractTable = "dms_extract";

    public override void Up()
    {
        // Defaulted, so rows loaded before this become the licence extract.
        Alter.Table(DmsExtractTable)
            .AddColumn("extract_type")
            .AsString()
            .NotNullable()
            .WithDefaultValue("AbstractionLicence");

        // Every read of the table filters on this.
        Create.Index("ix_dms_extract_extract_type")
            .OnTable(DmsExtractTable)
            .OnColumn("extract_type").Ascending();
    }

    public override void Down()
    {
        Delete.Index("ix_dms_extract_extract_type").OnTable(DmsExtractTable);
        Delete.Column("extract_type").FromTable(DmsExtractTable);
    }
}
