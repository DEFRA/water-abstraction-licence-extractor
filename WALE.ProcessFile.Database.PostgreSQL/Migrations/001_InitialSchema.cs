using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(1)]
public class InitialSchema : Migration
{
    public override void Up()
    {
        Create.Table("ProcessRun")
            .WithColumn("ProcessRunId").AsInt32().PrimaryKey().Identity()
            .WithColumn("Description").AsString().Nullable()
            .WithColumn("StartDateTimeUtc").AsDateTime().NotNullable()
            .WithColumn("EndDateTimeUtc").AsDateTime().Nullable()
            .WithColumn("NumberOfFiles").AsInt32().NotNullable();

        Create.Table("LicenceSet")
            .WithColumn("LicenceSetId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("SchemaLicenceSetId").AsString().NotNullable()
            .WithColumn("ShortLicenceSetId").AsString().NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable();

        Create.Table("Licence")
            .WithColumn("LicenceId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("Filename").AsString().NotNullable()
            .WithColumn("Data").AsString().NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable()
            .WithColumn("LicenceNumber").AsString().Nullable();

        Create.Table("LicenceSetType")
            .WithColumn("LicenceSetId").AsInt32().NotNullable()
            .WithColumn("LicenceSetType").AsInt32().NotNullable();

        Create.Table("NoOcrPageTextCache")
            .WithColumn("NoOcrPageTextCacheId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("Filename").AsString().NotNullable()
            .WithColumn("PageNumber").AsInt32().NotNullable()
            .WithColumn("NoOcrServiceName").AsString().NotNullable()
            .WithColumn("Data").AsString().NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable();

        Create.Table("NoOcrPagesMetadataCache")
            .WithColumn("NoOcrPagesMetadataCacheId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("Filename").AsString().NotNullable()
            .WithColumn("NoOcrServiceName").AsString().NotNullable()
            .WithColumn("Response").AsString().NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable();

        Create.Table("PageScreenshot")
            .WithColumn("PageScreenshotId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("Filename").AsString().NotNullable()
            .WithColumn("PageNumber").AsInt32().NotNullable()
            .WithColumn("NoOcrServiceName").AsString().NotNullable()
            .WithColumn("Data").AsBinary(int.MaxValue).NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable();

        Create.Table("AllPagesText")
            .WithColumn("AllPagesTextId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("Filename").AsString().NotNullable()
            .WithColumn("NoOcrServiceName").AsString().NotNullable()
            .WithColumn("Data").AsString().NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable();

        Create.Table("NoOcrImagesMetadataCache")
            .WithColumn("NoOcrImagesMetadataCacheId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("Filename").AsString().NotNullable()
            .WithColumn("NoOcrServiceName").AsString().NotNullable()
            .WithColumn("Response").AsString().NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable();

        Create.Table("ImageOnPage")
            .WithColumn("ImageOnPageId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("Filename").AsString().NotNullable()
            .WithColumn("NoOcrServiceName").AsString().NotNullable()
            .WithColumn("Data").AsBinary(int.MaxValue).NotNullable()
            .WithColumn("PageNumber").AsInt32().NotNullable()
            .WithColumn("ImageNumber").AsInt32().NotNullable()
            .WithColumn("Extension").AsString(5).NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable();

        Create.Table("OcrImageTextCache")
            .WithColumn("OcrImageTextCacheId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("Filename").AsString().NotNullable()
            .WithColumn("PageNumber").AsInt32().NotNullable()
            .WithColumn("ImageNumber").AsInt32().NotNullable()
            .WithColumn("OcrServiceName").AsString().NotNullable()
            .WithColumn("Data").AsString().NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable();

        Create.Table("MatchesResult")
            .WithColumn("MatchesResultId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("Filename").AsString().NotNullable()
            .WithColumn("Data").AsString().NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable();

        Create.Table("LicenceSetLicence")
            .WithColumn("LicenceSetLicenceId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("LicenceSetId").AsInt32().NotNullable()
            .WithColumn("LicenceNumber").AsString().NotNullable()
            .WithColumn("LicenceVersionId").AsString().NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable()
            .WithColumn("LicenceId").AsInt32().Nullable();

        Create.Table("AggregateSet")
            .WithColumn("AggregateSetId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("LicenceSetId").AsInt32().NotNullable()
            .WithColumn("SchemaAggregateSetId").AsString().NotNullable()
            .WithColumn("Data").AsString().NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable();

        Create.Table("OcrScreenshotTextCache")
            .WithColumn("OcrImageTextCacheId").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProcessRunId").AsInt32().NotNullable()
            .WithColumn("Filename").AsString().NotNullable()
            .WithColumn("PageNumber").AsInt32().NotNullable()
            .WithColumn("OcrServiceName").AsString().NotNullable()
            .WithColumn("Data").AsString().NotNullable()
            .WithColumn("DateTimeUtc").AsDateTime().NotNullable();

        Create.Table("Match")
            .WithColumn("MatchId").AsInt32().PrimaryKey().Identity()
            .WithColumn("MatchesResultId").AsInt32().NotNullable()
            .WithColumn("LabelName").AsString().Nullable()
            .WithColumn("LabelGroupName").AsString().Nullable()
            .WithColumn("Data").AsString().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Match");
        Delete.Table("OcrScreenshotTextCache");
        Delete.Table("AggregateSet");
        Delete.Table("LicenceSetLicence");
        Delete.Table("MatchesResult");
        Delete.Table("OcrImageTextCache");
        Delete.Table("ImageOnPage");
        Delete.Table("NoOcrImagesMetadataCache");
        Delete.Table("AllPagesText");
        Delete.Table("PageScreenshot");
        Delete.Table("NoOcrPagesMetadataCache");
        Delete.Table("NoOcrPageTextCache");
        Delete.Table("LicenceSetType");
        Delete.Table("Licence");
        Delete.Table("LicenceSet");
        Delete.Table("ProcessRun");
    }
}