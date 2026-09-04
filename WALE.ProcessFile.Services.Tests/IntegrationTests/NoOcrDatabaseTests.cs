using FakeItEasy;
using Meziantou.Xunit;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tests.Helper;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.DocumentType.AbstractionLicence.Formats;
using WRADI.DocumentType.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Services;
using WRADI.Services.Cache.AbstractionLicence;
using WRADI.Services.Output.AbstractionLicence;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[EnableParallelization]
public class NoOcrDatabaseTests
{
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider =
        new(TestConfig.PostgresHost,
            TestConfig.PostgresPort,
            TestConfig.PostgresDbName,
            TestConfig.PostgresUsername,
            TestConfig.PostgresPassword,
            maxPoolSize: 10);
    
    private static IDatabaseReadService ReadService =>
        new PostgresReadService(NpgsqlDataSourceProvider);

    private static IDatabaseWriteService WriteService =>
        new PostgresWriteService(NpgsqlDataSourceProvider);

    private static readonly ICacheService CacheService = new DatabaseCacheService(
        ReadService,
        WriteService);
    
    private static IAbstractionLicenceDatabaseReadService AbsLicReadService =>
        new PostgresAbstractionLicenceReadService(NpgsqlDataSourceProvider);

    private static IAbstractionLicenceDatabaseWriteService AbsLicWriteService =>
        new PostgresAbstractionLicenceWriteService(NpgsqlDataSourceProvider);
    
    private static readonly IAbstractionLicenceCacheService AbsLicCacheService =
        new DatabaseAbstractionLicenceCacheService(
            AbsLicReadService,
            AbsLicWriteService);
    
    private static readonly IAbstractionLicenceOutputService AbsLicOutputService =
        new DatabaseAbstractionLicenceOutputService(
            null!,
            AbsLicReadService,
            AbsLicWriteService,
            null!);
    
    private static readonly INaldDataLookupService NaldDataLookupService;
    private static readonly IOutputService OutputService = new DatabaseOutputService(ReadService, WriteService);
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = A.Fake<IMessageQueueService>(); 

    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>(),
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService,
        MessageQueueService);

    public NoOcrDatabaseTests()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    static NoOcrDatabaseTests()
    {
        NaldDataLookupService = new NaldDataLookupService(AbsLicCacheService, AbsLicOutputService);
    }
    
    private static async Task<ILicenceNumberService> GetLicenceNumbersAsync(short regionCode)
    {
        var allNaldData = await AbsLicCacheService.GetNaldDataAsync(regionCode, false, 0, int.MaxValue);
        return new AbstractionLicenceNumber(allNaldData.AbstractionAndImpoundmentLicences!, []);
    }

    private static Dictionary<string, DmsFileData> FileLicenceMapping =>
        new()
        {
            {
                "25 68 001 247",
                new DmsFileData { DestinationFileName = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10892721.pdf" }
            },
            {
                "25 68 001 248",
                new DmsFileData { DestinationFileName = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893422.pdf" }
            },
            {
                "NE/026/0034/018",
                new DmsFileData { DestinationFileName = "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf" }
            },
            {
                "NE/026/0034/052",
                new DmsFileData { DestinationFileName = "NE0260034052__Application Apportionment Issued Licence 11.12.2019 11149440.pdf" }
            }
        };

    private readonly Dictionary<string, List<NaldAbstractionData>> _naldData = [];

    private async Task<LookupConfiguration> LookupConfigurationAsync(string pdfFolder)
    {
        return new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            await CompanyNameHelper.GetFirstNamesCsvFromFileAsync(),
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            await GetLicenceNumbersAsync(3),
            new DmsLookupService(),
            3,
            DateTime.Now,
            useLockExclusivity: false);
    }
    
    private async Task<MatchesResult> GetMatchesAsync(string fileName, Guid fileId)
    {
        return (await _pdfDataExtractor.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = fileId },
            await LookupConfigurationAsync(TestConfig.PdfFolder),
            [fileName],
            0)).Item!;
    }
    
    [Fact]
    public async Task AddProcessRun()
    {
        // Arrange
        var processRun = await OutputService.StartProcessRunAsync(new ProcessRun
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
        var someGuid = Guid.NewGuid();
        
        await CacheService.ClearCacheAsync(someGuid);
        
        await ProcessAsync(filename, someGuid); // Uncached
        await ProcessAsync(filename, someGuid); // Cached
    }

    private async Task ProcessAsync(string filename, Guid fileId)
    {

        
        // Act
        var resultFull = await GetMatchesAsync(filename, fileId);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(10, records.Text!.Count);

        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(14, additionalInformation.Text!.Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);

        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");

        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Ingleby Greenhow Water Society Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        Assert.Equal(3, nameResult.LabelStartPageNumber);
        Assert.Equal(6, nameResult.LabelStartLineNumber);

        // Note no other licence mentioned
        var abstractionLimitsSection =
            resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");

        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(5, abstractionLimitsSection.Text!.Count);
        Assert.Equal("A day means any period of 24 consecutive hours and a year means the",
            abstractionLimitsSection.Text![3].Text);
        Assert.Equal(4, abstractionLimitsSection.LabelStartPageNumber);        
        Assert.Equal(15, abstractionLimitsSection.LabelStartLineNumber);

        Assert.NotNull(abstractionLimitsSection.SubResults);
        Assert.Single(abstractionLimitsSection.SubResults);

        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint1.SubResults);

        var point1Sub1 = abstractionLimitsPoint1.SubResults[0];
        Assert.NotNull(point1Sub1);
        Assert.Equal("AbstractionLimitPointSub", point1Sub1.MatchedLabel?.Name);

        Assert.Equal(4, point1Sub1.Text!.Count);

        Assert.NotNull(point1Sub1.SubResults);
        Assert.Equal(6, point1Sub1.SubResults.Count);

        var perDay = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(4, perDay.LabelStartPageNumber);
        Assert.Equal(16, perDay.LabelStartLineNumber);
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
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()
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
        Assert.Equal(3, licenceNumberResult.LabelStartPageNumber);
        Assert.Equal(0, licenceNumberResult.LabelStartLineNumber);

        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal(
            "4. PURPOSES OF ABSTRACTION 4.1 Private Water Supply. 4.2 Agriculture (other than Spray Irrigation).",
            string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!));
        Assert.Equal(["PURPOSES OF ABSTRACTION"], purposeResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.BetweenLabels, purposeResult.MatchedPosition);

        Assert.Single(purposeResult.SubResults);

        var firstPurposePointGroup = purposeResult.SubResults.First();
        var firstPurpose = firstPurposePointGroup.SubResults[0];

        Assert.Equal("Purposes", firstPurpose.MatchedLabel!.Name);
        Assert.Equal("4.1 Private Water Supply.", firstPurpose.Text!.First().Text);
        Assert.Equal(2, firstPurpose.SubResults.Count);

        var firstPurposeWithoutPrepoint = firstPurpose.SubResults[1];
        Assert.Equal("Private Water Supply", firstPurposeWithoutPrepoint.Text!.First().Text);

        var secondPurpose = firstPurposePointGroup.SubResults[1];
        Assert.Equal("4.2 Agriculture (other than Spray Irrigation).", secondPurpose.Text!.First().Text);

        var secondPurposeWithoutPrepoint = secondPurpose.SubResults[1];
        Assert.Equal("Agriculture (other than Spray Irrigation)", secondPurposeWithoutPrepoint.Text!.First().Text);

        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(TestConfig.PdfFolder),
            AbsLicCacheService, NaldDataLookupService);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.Single();

        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("1/25/04/059", agreedSchemaLicence.LicenceNumber?.Value);

        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual![0].Limits.Count);

        var limitG = agreedSchemaLicence.AbstractionLimits.Individual[0];
        var limit = limitG.Limits[0];

        Assert.Equal(LimitPeriodType.PerDay, limit.PeriodType);
        Assert.Equal("cubic metres", limit.Units);
        Assert.Equal(90.91, limit.Value);
        Assert.Null(limit.Points!);
        Assert.Single(limitG.Points!);        
        Assert.Equal(0, limitG.Points!.Count(c => c.IsImplicit != true));
        Assert.Equal(0, limitG.Purposes!.Count(c => c.IsImplicit != true));

        limit = limitG.Limits[1];
        Assert.Equal(LimitPeriodType.PerYear, limit.PeriodType);
        Assert.Equal("cubic metres", limit.Units);
        Assert.Equal(33182, limit.Value);
        Assert.Null(limit.Points!);
        Assert.Single(limitG.Points!);
        Assert.Equal(0, limitG.Points!.Count(c => c.IsImplicit != true));
        Assert.Equal(0, limitG.Purposes!.Count(c => c.IsImplicit != true));

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