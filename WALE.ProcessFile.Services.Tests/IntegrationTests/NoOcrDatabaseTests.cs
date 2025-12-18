using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.ProcessFile.Services.Tests.Helper;
using MatchType = WALE.ProcessFile.Core.Enums.MatchType;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

public class NoOcrDatabaseTests
{
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider =
        new(TestConfig.PostgresConnectionString);
    
    private static IDatabaseReadService ReadService =>
        new PostgresReadService(NpgsqlDataSourceProvider);

    private static IDatabaseWriteService WriteService =>
        new PostgresWriteService(NpgsqlDataSourceProvider);

    private static readonly ICacheService CacheService = new DatabaseCacheService(ReadService, WriteService);
    private static readonly IOutputService OutputService = new DatabaseOutputService(ReadService, WriteService);

    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>(),
        CacheService,
        OutputService,
        TestConfig.PdfFolder);

    public NoOcrDatabaseTests()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private static Dictionary<string, string> FileLicenceMapping =>
        new()
        {
            {
                "25 68 001 247",
                "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10892721.pdf"
            },
            {
                "25 68 001 248",
                "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893422.pdf"
            },
            {
                "NE/026/0034/018",
                "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf"
            },
            {
                "NE/026/0034/052",
                "NE0260034052__Application Apportionment Issued Licence 11.12.2019 11149440.pdf"
            }
        };

    private readonly HashSet<string> _liveLicenceNumbers = [];
    private readonly HashSet<string> _deadLicenceNumbers = [];
    private readonly HashSet<string> _impoundmentLicenceNumbers = [];
    
    private Task<MatchesResult> GetMatchesAsync(string fileName, bool useMainPdfFolder = true)
    {
        return _pdfDataExtractor.GetMatchesAsync(
            TestConfig.PdfFolder + fileName,
            new LookupConfiguration(
                LabelConfiguration.GetLabels(),
                FileLicenceMapping),
            [TestConfig.PdfFolder + fileName],
            0);
    }
    
    [Fact]
    public async Task AddProcessRun()
    {
        // Arrange
        var processRun = await OutputService.SaveProcessRunAsync(new ProcessRun
        {
            Description = "Test run",
            StartDateTimeUtc = DateTime.UtcNow,
            EndDateTimeUtc = DateTime.UtcNow.AddHours(2),
            NumberOfFiles = 19
        });
        
        Assert.NotEqual(0, processRun.ProcessRunId);
    }
    
    [Fact]
    public async Task Uncached_Then_Changed()
    {
        // Arrange
        const string filename = "Application –Transfer– Issued Licence –05072022.pdf";
        await CacheService.ClearCacheAsync(filename);
        
        await ProcessAsync(filename); // Uncached
        await ProcessAsync(filename); // Cached
    }

    private async Task ProcessAsync(string filename)
    {
        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(14, GeneralTeststHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(10, records.Text!.Count);

        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(16, additionalInformation.Text!.Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);

        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");

        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Ingleby Greenhow Water Society Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        Assert.Equal(59, nameResult.LineNumber);

        // Note no other licence mentioned
        var abstractionLimitsSection =
            resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");

        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(4, abstractionLimitsSection.Text!.Count);
        Assert.Equal("A day means any period of 24 consecutive hours and a year means the",
            abstractionLimitsSection.Text![2].Text);
        Assert.Equal(109, abstractionLimitsSection.LineNumber);

        Assert.NotNull(abstractionLimitsSection.SubResults);
        Assert.Single(abstractionLimitsSection.SubResults);

        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint1.SubResults);

        var point1Sub1 = abstractionLimitsPoint1.SubResults[0];
        Assert.NotNull(point1Sub1);
        Assert.Equal("AbstractionLimitPointSub", point1Sub1.MatchedLabel?.Name);

        Assert.Equal(4, point1Sub1.Text!.Count);

        Assert.NotNull(point1Sub1.SubResults);
        Assert.Equal(5, point1Sub1.SubResults.Count);

        var perDay = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(109, perDay.LineNumber);
        Assert.Equal("90.91", perDay.Text?.FirstOrDefault()?.Text);

        var perDayUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()
            ?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        var perYear = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()
            ?.Text;
        Assert.Equal("33182", perYear);

        var perYearUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()
            ?.Text;
        Assert.Equal("cubic metres", perYearUnits);

        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");

        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("1/25/04/059", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(53, licenceNumberResult.LineNumber);

        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal(
            "4. PURPOSES OF ABSTRACTION 4.1 Private Water Supply. 4.2 Agriculture (other than Spray Irrigation).",
            string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!));
        Assert.Equal(["PURPOSES OF ABSTRACTION"], purposeResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchType.Between, purposeResult.MatchType);

        Assert.Single(purposeResult.SubResults);

        var firstPurposePointGroup = purposeResult.SubResults.First();
        var firstPurpose = firstPurposePointGroup.SubResults[0];

        Assert.Equal("Purpose", firstPurpose.MatchedLabel!.Name);
        Assert.Equal("4.1 Private Water Supply.", firstPurpose.Text!.First().Text);
        Assert.Equal(2, firstPurpose.SubResults.Count);

        var firstPurposeWithoutPrepoint = firstPurpose.SubResults[1];
        Assert.Equal("Private Water Supply", firstPurposeWithoutPrepoint.Text!.First().Text);

        var secondPurpose = firstPurposePointGroup.SubResults[1];
        Assert.Equal("4.2 Agriculture (other than Spray Irrigation).", secondPurpose.Text!.First().Text);

        var secondPurposeWithoutPrepoint = secondPurpose.SubResults[1];
        Assert.Equal("Agriculture (other than Spray Irrigation)", secondPurposeWithoutPrepoint.Text!.First().Text);

        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            FileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractor,
            TestConfig.PdfFolder,
            0);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.Single();

        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("1/25/04/059", agreedSchemaLicence.LicenceNumber);

        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual![0].Limits.Count);

        var limitG = agreedSchemaLicence.AbstractionLimits.Individual[0];
        var limit = limitG.Limits[0];

        Assert.Equal(LimitPeriodType.PerDay, limit.PeriodType);
        Assert.Equal("cubic metres", limit.Units);
        Assert.Equal(90.91, limit.Value);
        Assert.Null(limit.Points);
        Assert.Null(limit.Purposes);

        limit = limitG.Limits[1];
        Assert.Equal(LimitPeriodType.PerYear, limit.PeriodType);
        Assert.Equal("cubic metres", limit.Units);
        Assert.Equal(33182, limit.Value);
        Assert.Null(limit.Points);
        Assert.Null(limit.Purposes);

        Assert.NotNull(agreedSchemaLicence.LicenceVersion);
        Assert.Equal("LV20220705", agreedSchemaLicence.LicenceVersion.LicenceVersionId);

        Assert.Null(agreedSchemaLicence.LicenceVersion.ExpiryDate);
        Assert.Equal(new DateTime(2022, 07, 05), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Equal(new DateTime(1968, 05, 15), agreedSchemaLicence.LicenceVersion.OriginalIssueDate);
        Assert.Equal(new DateTime(2022, 07, 05), agreedSchemaLicence.LicenceVersion.IssueDate);

        Assert.Equal("12504059-LV20220705", agreedSchemaLicenceGroup.Last().LicenceSetId);

        Assert.NotNull(agreedSchemaLicenceGroup.Last().Licences);
        Assert.Single(agreedSchemaLicenceGroup.Last().Licences);

        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Null(agreedSchemaLicenceGroup.Last().AggregateSets);
    }
}