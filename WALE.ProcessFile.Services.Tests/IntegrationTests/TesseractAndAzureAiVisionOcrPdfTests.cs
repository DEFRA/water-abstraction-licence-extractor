using System.Text.RegularExpressions;
using FakeItEasy;
using Meziantou.Xunit;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WALE.ProcessFile.Services.Tests.Helper;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.Services.Cache.AbstractionLicence;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[EnableParallelization]
[Collection("First Names 4")]
public partial class TesseractAndAzureAiVisionOcrPdfTests(SingletonFirstNamesFixture firstNamesFixture)
{
    private static readonly ICacheService CacheService;
    private static readonly IAbstractionLicenceCacheService AbsLicCacheService;

    static TesseractAndAzureAiVisionOcrPdfTests()
    {
        var realCacheService = new FileSystemCacheService("Cache/");
        var realAbsLicCacheService = new FileSystemAbstractionLicenceCacheService("Cache/");

        (CacheService, AbsLicCacheService) = GeneralTestsHelper.GetFakeCacheService(
            realCacheService,
            realAbsLicCacheService,
            _naldData,
            _fileLicenceMapping);
    }
    
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider =
        new(TestConfig.PostgresHost,
            TestConfig.PostgresPort,
            TestConfig.PostgresDbName,
            TestConfig.PostgresUsername,
            TestConfig.PostgresPassword);
    
    private static IAbstractionLicenceDatabaseReadService ReadService =>
        new PostgresAbstractionLicenceReadService(NpgsqlDataSourceProvider);

    private static readonly IAbstractionLicenceCacheService DatabaseCacheService =
        new DatabaseAbstractionLicenceCacheService(ReadService, null!);
    
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = A.Fake<IMessageQueueService>(); 
    
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.SparseTextOsd, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.Auto, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
            new AzureAiVisionOcrDataExtractorService(
                TestConfig.AiVisionEndpoint,
                TestConfig.AiVisionKey,
                CacheService,
                OutputService)
        },
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService,
        MessageQueueService);
    
    private static readonly Dictionary<string, DmsFileData> _fileLicenceMapping = new()
    {
        {
            "2_27_22_210", 
            new DmsFileData
            {
                DestinationFileName = "22722210__Application Formal Variation Issued Licence - 27.03.2025.pdf",
                DmsPath = "TEST_FAKE_PATH",
                FileId = Guid.NewGuid(),
            }
        }
    };

    private static readonly Dictionary<string, List<NaldData>> _naldData = [];

    private async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode, string pdfFolder)
    {
        return new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            await firstNamesFixture.FirstNamesCsvTask(),
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            await firstNamesFixture.GetLicenceNumbersServiceAsync((short)regionCode, DatabaseCacheService),
            regionCode,
            DateTime.Now,
            useLockExclusivity: false);
    }

    private async Task<MatchesResult> GetMatchesAsync(string fileName, int useExtractor = 1, int regionCode = 3)
    {
        string f;

        switch (useExtractor)
        {
            case 1:
                f = TestConfig.PdfFolder;
                break;
            case 3:
                f = TestConfig.PdfFolder3;
                break;
            case 4:
                f = TestConfig.PdfFolder4;
                break;
            case 5:
                f = TestConfig.PdfFolder5;
                break;            
            default:
                throw new Exception("Number not known");
        }
        
        return (await _pdfDataExtractor.GetMatchesAsync(
             fileName,
             new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            await LookupConfigurationAsync(regionCode, f),
            [fileName],
            0)).Item!;
    }
    
    [Fact]
    public async Task WhenOldEaFile_ThenGetAbstractionLimitsFromOtherConditionsCorrectly()
    {
        // Arrange
        var regionCode = 5;

        const string filename = "12301001__1 23 01 001 Hallington - Licence Document.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, regionCode: regionCode);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(13, resultList.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("7 March 2007", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResult);
        Assert.Equal("Northumbrian Water Ltd", nameResult.Text?.First().Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);
        
        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);
        
        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults[0];
        
        Assert.Equal(9, section1Sub1.SubResults!.Count);

        var pointName = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition")?.Text!.First().Text;
        
        Assert.Null(pointName);
        
        var perYearUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("thousand cubic metres", perYearUnits?.Text?.FirstOrDefault()?.Text);

        var perYearValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("66363570", perYearValue?.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("cubic metres", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("181818", perDayValue?.Text?.FirstOrDefault()?.Text);

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("cubic metres", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("7575", perHourValue?.Text?.FirstOrDefault()?.Text);
        
        perYearUnits = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("thousand cubic metres", perYearUnits?.Text?.FirstOrDefault()?.Text);

        perYearValue = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("66363570", perYearValue?.Text?.FirstOrDefault()?.Text); // TODO should be 1364
        
        perDayUnits = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("cubic metres", perDayUnits?.Text?.FirstOrDefault()?.Text);

        perDayValue = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("181818", perDayValue?.Text?.FirstOrDefault()?.Text); 

        var otherConditionsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "OtherConditions");
        Assert.NotNull(otherConditionsResult);
        Assert.Equal(23, otherConditionsResult.Text?.Count);
        
        Assert.Equal(8, otherConditionsResult.SubResults.Count);

        // 1st
        var otherConditionsPoint = otherConditionsResult.SubResults[0];
        Assert.Equal(2, otherConditionsPoint.Text?.Count);
        Assert.Equal("OtherConditionsPoint", otherConditionsPoint.MatchedLabel?.Name);
        Assert.Equal("1. That the abstraction from Catcleugh Reservoir shall not exceed 63,645 cubic", otherConditionsPoint.Text?.FirstOrDefault()?.Text);
        
        Assert.Single(otherConditionsPoint.SubResults);
        var otherConditionsPointSub = otherConditionsPoint.SubResults[0];
        Assert.Equal("AbstractionLimitPointSub", otherConditionsPointSub.MatchedLabel?.Name);
        Assert.Equal("1. That the abstraction from Catcleugh Reservoir shall not exceed 63,645 cubic", otherConditionsPointSub.Text?.FirstOrDefault()?.Text);
        
        Assert.Equal(3, otherConditionsPointSub.SubResults.Count);
        Assert.Equal("DocumentIdentifier", otherConditionsPointSub.SubResults[0].MatchedLabel?.Name);
        Assert.Equal("1", otherConditionsPointSub.SubResults[0].Text?.FirstOrDefault()?.Text);
        Assert.Equal("PerDayUnits", otherConditionsPointSub.SubResults[1].MatchedLabel?.Name);
        Assert.Equal("cubic metres", otherConditionsPointSub.SubResults[1].Text?.FirstOrDefault()?.Text);
        Assert.Equal("PerDayValue", otherConditionsPointSub.SubResults[2].MatchedLabel?.Name);
        Assert.Equal("63645", otherConditionsPointSub.SubResults[2].Text?.FirstOrDefault()?.Text);
        
        // 2nd
        otherConditionsPoint = otherConditionsResult.SubResults[1];
        Assert.Equal(2, otherConditionsPoint.Text?.Count);
        Assert.Equal("OtherConditionsPoint", otherConditionsPoint.MatchedLabel?.Name);
        Assert.Equal("2. There shall be a continuous compensation flow of not less than 571 cubic", otherConditionsPoint.Text?.FirstOrDefault()?.Text);
        
        Assert.Single(otherConditionsPoint.SubResults);
        otherConditionsPointSub = otherConditionsPoint.SubResults[0];
        Assert.Equal("AbstractionLimitPointSub", otherConditionsPointSub.MatchedLabel?.Name);
        Assert.Equal("2. There shall be a continuous compensation flow of not less than 571 cubic", otherConditionsPointSub.Text?.FirstOrDefault()?.Text);
        
        Assert.Equal(3, otherConditionsPointSub.SubResults.Count);
        Assert.Equal("DocumentIdentifier", otherConditionsPointSub.SubResults[0].MatchedLabel?.Name);
        Assert.Equal("2", otherConditionsPointSub.SubResults[0].Text?.FirstOrDefault()?.Text);
        Assert.Equal("PerHourUnits", otherConditionsPointSub.SubResults[1].MatchedLabel?.Name);
        Assert.Equal("cubic metres", otherConditionsPointSub.SubResults[1].Text?.FirstOrDefault()?.Text);
        Assert.Equal("PerHourValue", otherConditionsPointSub.SubResults[2].MatchedLabel?.Name);
        Assert.Equal("571", otherConditionsPointSub.SubResults[2].Text?.FirstOrDefault()?.Text);

        // 3rd
        otherConditionsPoint = otherConditionsResult.SubResults[2];
        Assert.Equal(2, otherConditionsPoint.Text?.Count);
        Assert.Equal("OtherConditionsPoint", otherConditionsPoint.MatchedLabel?.Name);
        Assert.Equal("3. That the abstraction from Colt Crag Reservoir and Little Swinburn Reservoir", otherConditionsPoint.Text?.FirstOrDefault()?.Text);
        
        Assert.Single(otherConditionsPoint.SubResults);
        otherConditionsPointSub = otherConditionsPoint.SubResults[0];
        Assert.Equal("AbstractionLimitPointSub", otherConditionsPointSub.MatchedLabel?.Name);
        Assert.Equal("3. That the abstraction from Colt Crag Reservoir and Little Swinburn Reservoir", otherConditionsPointSub.Text?.FirstOrDefault()?.Text);
        
        Assert.Equal(3, otherConditionsPointSub.SubResults.Count);
        Assert.Equal("DocumentIdentifier", otherConditionsPointSub.SubResults[0].MatchedLabel?.Name);
        Assert.Equal("3", otherConditionsPointSub.SubResults[0].Text?.FirstOrDefault()?.Text);
        Assert.Equal("PerDayUnits", otherConditionsPointSub.SubResults[1].MatchedLabel?.Name);
        Assert.Equal("cubic metres", otherConditionsPointSub.SubResults[1].Text?.FirstOrDefault()?.Text);
        Assert.Equal("PerDayValue", otherConditionsPointSub.SubResults[2].MatchedLabel?.Name);
        Assert.Equal("90922", otherConditionsPointSub.SubResults[2].Text?.FirstOrDefault()?.Text);
        
        // 4th
        otherConditionsPoint = otherConditionsResult.SubResults[3];
        Assert.Equal(2, otherConditionsPoint.Text?.Count);
        Assert.Equal("OtherConditionsPoint", otherConditionsPoint.MatchedLabel?.Name);
        Assert.Equal("4. There shall be a continuous compensation flow of not less than 38 cubic", otherConditionsPoint.Text?.FirstOrDefault()?.Text);
        
        Assert.Single(otherConditionsPoint.SubResults);
        otherConditionsPointSub = otherConditionsPoint.SubResults[0];
        Assert.Equal("AbstractionLimitPointSub", otherConditionsPointSub.MatchedLabel?.Name);
        Assert.Equal("4. There shall be a continuous compensation flow of not less than 38 cubic", otherConditionsPointSub.Text?.FirstOrDefault()?.Text);
        
        Assert.Equal(3, otherConditionsPointSub.SubResults.Count);
        Assert.Equal("DocumentIdentifier", otherConditionsPointSub.SubResults[0].MatchedLabel?.Name);
        Assert.Equal("4", otherConditionsPointSub.SubResults[0].Text?.FirstOrDefault()?.Text);
        Assert.Equal("PerHourUnits", otherConditionsPointSub.SubResults[1].MatchedLabel?.Name);
        Assert.Equal("cubic metres", otherConditionsPointSub.SubResults[1].Text?.FirstOrDefault()?.Text);
        Assert.Equal("PerHourValue", otherConditionsPointSub.SubResults[2].MatchedLabel?.Name);
        Assert.Equal("38", otherConditionsPointSub.SubResults[2].Text?.FirstOrDefault()?.Text);
        
        // 5th
        otherConditionsPoint = otherConditionsResult.SubResults[4];
        Assert.Equal(4, otherConditionsPoint.Text?.Count);
        Assert.Equal("OtherConditionsPoint", otherConditionsPoint.MatchedLabel?.Name);
        Assert.Equal("5. To enable the compensation flow from the reservoirs to be measured,", otherConditionsPoint.Text?.FirstOrDefault()?.Text);
        
        Assert.Empty(otherConditionsPoint.SubResults);
        
        // 6th
        otherConditionsPoint = otherConditionsResult.SubResults[5];
        Assert.Equal(7, otherConditionsPoint.Text?.Count);
        Assert.Equal("OtherConditionsPoint", otherConditionsPoint.MatchedLabel?.Name);
        Assert.Equal("6. Neither the total abstraction from East and West Hallington Reservoirs nor the", otherConditionsPoint.Text?.FirstOrDefault()?.Text);
        
        Assert.Equal(2, otherConditionsPoint.SubResults.Count);
        
        var otherConditionsLinkedLicenceNumber = otherConditionsPoint.SubResults[0];
        Assert.Equal("OtherConditionsLinkedLicenceNumber", otherConditionsLinkedLicenceNumber.MatchedLabel?.Name);
        Assert.Equal("1/23/01/159", otherConditionsLinkedLicenceNumber.Text?.FirstOrDefault()?.Text);
        
        otherConditionsPointSub = otherConditionsPoint.SubResults[1];
        Assert.Equal("AbstractionLimitPointSub", otherConditionsPointSub.MatchedLabel?.Name);
        Assert.Equal("6. Neither the total abstraction from East and West Hallington Reservoirs nor the", otherConditionsPointSub.Text?.FirstOrDefault()?.Text);
        
        Assert.Equal(4, otherConditionsPointSub.SubResults.Count);
        Assert.Equal("DocumentIdentifier", otherConditionsPointSub.SubResults[0].MatchedLabel?.Name);
        Assert.Equal("6", otherConditionsPointSub.SubResults[0].Text?.FirstOrDefault()?.Text);
        Assert.Equal("LinkedLicenceNumber", otherConditionsPointSub.SubResults[1].MatchedLabel?.Name);
        Assert.Equal("1/23/01/159", otherConditionsPointSub.SubResults[1].Text?.FirstOrDefault()?.Text);
        Assert.Equal("PerDayUnits", otherConditionsPointSub.SubResults[2].MatchedLabel?.Name);
        Assert.Equal("cubic metres", otherConditionsPointSub.SubResults[2].Text?.FirstOrDefault()?.Text);
        Assert.Equal("PerDayValue", otherConditionsPointSub.SubResults[3].MatchedLabel?.Name);
        Assert.Equal("181818", otherConditionsPointSub.SubResults[3].Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("1/23/01/001", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder),
            AbsLicCacheService);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.First().Licences[0];
        
        Assert.Equal("1/23/01/001", agreedSchemaLicence.LicenceNumber!.Value);
        
        Assert.Equal(7, agreedSchemaLicence.Points.Length);
        Assert.Equal("Catcleugh Reservoir", agreedSchemaLicence.Points[0].Name);
        Assert.NotNull(agreedSchemaLicence.Points[0].ContainedIn);
        Assert.Single(agreedSchemaLicence.Points[0].ContainedIn!);
        Assert.Equal("SourceOfSupply", agreedSchemaLicence.Points[0].ContainedIn![0].SectionName);

        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Aggregates!.Length);
        
        Assert.Equal(4, agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits.Count);
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[0].Units);
        Assert.Equal(7575, agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[0].Value);
        Assert.Equal(181818, agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[1].Value);
        Assert.Equal("thousand cubic metres", agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[2].Units);
        Assert.Equal(66363570, agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[2].Value);
        Assert.Equal("litres", agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[3].Units);
        Assert.Equal(2.1, agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[3].Value);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.AbstractionLimits.Aggregates![0].ContainedIn![0].SectionName);
        Assert.Null( agreedSchemaLicence.AbstractionLimits.Aggregates![0].ContainedIn![0].LinkReason);
        Assert.Empty(agreedSchemaLicence.AbstractionLimits.Aggregates![0].LinkedLicences!);
        Assert.Equal(7, agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[0].Points!.Length);
        Assert.Equal(0, agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[0].Points!.Count(p => p.IsImplicit != true));
        
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates![1].Limits);
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Aggregates![1].Limits[0].Units);
        Assert.Equal(90922, agreedSchemaLicence.AbstractionLimits.Aggregates![1].Limits[0].Value);
        Assert.Equal("OtherConditions", agreedSchemaLicence.AbstractionLimits.Aggregates![1].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", agreedSchemaLicence.AbstractionLimits.Aggregates![1].ContainedIn![0].LinkReason);
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates![1].LinkedLicences);
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates![1].Limits[0].Points!);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates![1].Points!.Length);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates![1].Points!.Count(c => c.IsImplicit != true));
        
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates![2].Limits);
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Aggregates![2].Limits[0].Units);
        Assert.Equal(181818, agreedSchemaLicence.AbstractionLimits.Aggregates![2].Limits[0].Value);
        Assert.Equal("OtherConditions", agreedSchemaLicence.AbstractionLimits.Aggregates![2].ContainedIn![0].SectionName);
        Assert.Equal("AuthorisedBy", agreedSchemaLicence.AbstractionLimits.Aggregates![2].ContainedIn![0].LinkReason);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates![2].LinkedLicences!);
        Assert.Equal("1/23/01/159", agreedSchemaLicence.AbstractionLimits.Aggregates![2].LinkedLicences![0]);
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates![2].Limits[0].Points!);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates![2].Points!.Length);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates![2].Points!.Count(c => c.IsImplicit != true));
        
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual!);
        
        // Abstraction limits section
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual![0].Limits);
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Individual![0].Limits[0].Units);
        Assert.Equal(63645, agreedSchemaLicence.AbstractionLimits.Individual![0].Limits[0].Value);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual![0].Limits[0].Points!);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("1/23/01/159", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
    }
    
    [Fact]
    public async Task When22722027__ThenFoundCorrectly()
    {
        // Arrange
        var regionCode = 5;

        const string filename = "22722027__2-27-22-027 6070677.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 5, regionCode: regionCode);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("1 June 2007", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResult);
        
        Assert.Equal("YORKSHIRE WATER SERVICES LIMITED", nameResult.Text?.First().Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult);
        Assert.Equal(14, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);
        
        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);
        
        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(8, section1Sub1.SubResults.Count);

        var pointName = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition")?.Text!.First().Text;
        
        Assert.Null(pointName);
        
        var perYearUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("thousand cubic metres", perYearUnits?.Text?.FirstOrDefault()?.Text);

        var perYearValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("3500", perYearValue?.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("cubic metres", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("3500", perDayValue?.Text?.FirstOrDefault()?.Text);

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("cubic metres", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Null(perHourValue?.Text?.FirstOrDefault()?.Text);
        
        perYearUnits = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("thousand cubic metres", perYearUnits?.Text?.FirstOrDefault()?.Text);

        perYearValue = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("3500", perYearValue?.Text?.FirstOrDefault()?.Text); // TODO should be 1364
        
        perDayUnits = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("cubic metres", perDayUnits?.Text?.FirstOrDefault()?.Text);

        perDayValue = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("3500", perDayValue?.Text?.FirstOrDefault()?.Text); // TODO should be 5.7

        perHourUnits = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("cubic metres", perHourUnits?.Text?.FirstOrDefault()?.Text);

        perHourValue = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Null(perHourValue?.Text?.FirstOrDefault()?.Text);

        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("2/27/22/027", licenceNumberResult.Text!.FirstOrDefault()?.Text);

        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder5);
        
        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.First().Licences.First();
        
        Assert.Equal("2/27/22/027", agreedSchemaLicence.LicenceNumber!.Value);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("2/27/22/210", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Individual);

        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates!);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits);
        Assert.Equal(3500, agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[0].Value);
        Assert.Equal("thousand cubic metres", agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[0].Units);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates![0].LinkedLicences!);
        Assert.Equal("2/27/22/210", agreedSchemaLicence.AbstractionLimits.Aggregates![0].LinkedLicences![0]);

        var licenceSetGroups1 = new List<IReadOnlyList<LicenceSet>> { agreedSchemaLicenceGroup };

        var licenceSetGroups = await AbstractionLicenceSchemaConverter.AddAdditionalLicenceSetsAsync(
            licenceSetGroups1,
            config,
            AbsLicCacheService);
        
        AbstractionLicenceSchemaConverter.CalculateCombinedAggregates(licenceSetGroups);
        
        var agreedSchemaLicence2 = licenceSetGroups.First().Licences.First();
        Assert.Equal(13187500, agreedSchemaLicence2.AbstractionLimits.Aggregates![0].Limits[0].Value);
    }
    
    [Fact]
    public async Task WhenIsOldCrossedOut_ThenFoundCorrectly()
    {
        // Arrange
        var regionCode = 5;

        const string filename = "Licence - Old 6082700.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, regionCode: regionCode);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(9, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("MERSEY AND WEAVER RIVER AUTHORITY", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("third day of April, 19 70", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResult);
        // Is crossed out but Azure AI can read it
        Assert.Equal("WARRINGTON, RUNCORN AND DISTRICT WATER BOARD", nameResult.Text?.First().Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult); // Is crossed out but Azure AI can read it
        Assert.Equal(9, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);
        
        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);
        
        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(14, section1Sub1.SubResults.Count);

        var pointName = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition")?.Text!.First().Text;
        
        Assert.Equal("(1)", pointName);
        
        var perYearUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("million gallons", perYearUnits?.Text?.FirstOrDefault()?.Text);

        var perYearValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("300", perYearValue?.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("million gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("1.25", perDayValue?.Text?.FirstOrDefault()?.Text);

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("thousand gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("52", perHourValue?.Text?.FirstOrDefault()?.Text);
        
        perYearUnits = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("megalitres", perYearUnits?.Text?.FirstOrDefault()?.Text);

        perYearValue = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("300", perYearValue?.Text?.FirstOrDefault()?.Text); // TODO should be 1364
        
        perDayUnits = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("megalitres", perDayUnits?.Text?.FirstOrDefault()?.Text);

        perDayValue = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("1.25", perDayValue?.Text?.FirstOrDefault()?.Text); // TODO should be 5.7

        perHourUnits = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("cubic metres", perHourUnits?.Text?.FirstOrDefault()?.Text);

        perHourValue = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("52", perHourValue?.Text?.FirstOrDefault()?.Text); // TODO should be 236

        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/68/3/91", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder),
            AbsLicCacheService);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("25/68/5/9", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("25/69/3/91", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal("25/68/3/76", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
    }
    
    [Fact]
    public async Task Handsigned_WhenNearPreviousLineIsCompany_ThenFoundCorrect_Ish()
    {
        // Arrange
        var regionCode = 5; // TODO Hampshire and IOW

        const string filename = "Non-Application Licence Document (22.09.1986).PDF";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, regionCode: regionCode);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(8, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("22ND DAY OF SEPTEMBER 1986", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal(11, nameResult.LabelStartLineNumber);
        // NOTE - According to companies house this is actually H.N. BUTLER FARMS LIMITED        
        Assert.Equal("H. W. Butter Farms Ltd", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("( hereinafter referred to as \"The Licence Holder\" )", nameResult.MatchedLabel!.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.OnOrNearPreviousLine, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);
        
        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(6, section1Sub1.SubResults!.Count);

        var inTotalUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "InTotalUnits");
        Assert.Equal("gallons", inTotalUnits?.Text?.FirstOrDefault()?.Text);

        var inTotalValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "InTotalValue");
        Assert.Equal("500000", inTotalValue?.Text?.FirstOrDefault()?.Text);      
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("36000", perDayValue?.Text?.FirstOrDefault()?.Text);

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1500", perHourValue?.Text?.FirstOrDefault()?.Text);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("11/42/28.2/7", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder),
            AbsLicCacheService);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("11/42/28.2/49", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
    }
    
    [Theory]
    [InlineData("12100004__Application Transfer Issued Licence - [1982] - (1982).pdf", "7 DAY OF OCTOBER 19 82", "07/10/1982", 4, 0, 1)]
    [InlineData("12100052__Application Formal Variation Issued Licence - [1987] - (1987).pdf", "2nd day of JUNE, 19 67", "02/06/1967", 5, 0, 1)]
    [InlineData("12100065__Application New Licence Issued - [1974] - (1974).pdf", "21st day of March 1974", "21/03/1974", 7, 0, 1)]
    [InlineData("12201014__Application New Licence Issued - [1966] - (1966).pdf", "27th day of JULY, 19 66", "27/07/1966", 7, 0, 1)]
    [InlineData("12201021__Application New Licence Issued - [1966] - (1966).pdf", "28th day of JULY, 19 6g", "28/07/1966", 6, 0, 1)]
    [InlineData("12201023__Application New Licence Issued - [1966] - (1966).pdf", "28th day of JULY, 19 66", "28/07/1966", 6, 0, 1)]
    [InlineData("12202043__abstraction license 1975.pdf", "14th day of February 1575", "14/02/1975", 5, 0, 1)]
    [InlineData("12203007__1-22-03-007 5822413.PDF", "9th day of MARCH, 1986", "09/03/1986", 5, 0, 1)]
    [InlineData("12203045__Non-Application Licence Document [Original licence] (23051966).PDF", "2 3rd day of MAY, 19 66", "23/05/1966", 7, 0, 1)]
    [InlineData("12203120__1-22-03-120 5822437.PDF", "6 September 2006", "06/09/2006", 11, 0, 1)]
    [InlineData("12205021__Original Licence 5684532.pdf", "5 DAY OF april 19 82", "05/04/1982", 6, 0, 1)]
    [InlineData("12205044__Non-Application Licence Document [Original Licence] (14101966).pdf", "14IEH day of OCTOBER, 1966", "14/10/1966", 6, 0, 1)]
    [InlineData("12301067__Application New Licence Issued - [1966] - (01081966).pdf", "1st day of AUGUST , 19 66", "01/08/1966", 6, 0, 1)]
    [InlineData("12302006__Licence Document 10031966.pdf", "day of MARCH, 1966", "01/03/1966", 7, 0, 1)]
    [InlineData("12302044__Non-Application Licence Document [Original Licence] (27.05.1966).PDF", "27th day of MAY 1966", "27/05/1966", 7, 0, 1)]
    [InlineData("12302207__1-23-02-207 5822808.PDF", "29th day of June 1976", "29/06/1976", 5, 0, 1)]
    [InlineData("12303008__Non-Application Licence Document [Original Licence] (11051966).PDF", "11 th day of NAY, 19 66", "11/05/1966", 6, 0, 1)]
    [InlineData("12303075__Non-Application Licence Document [Original Licence] (08111966).PDF", "8th day of NOVEMBER, 19 66", "08/11/1966", 7, 0, 1)]
    [InlineData("12202009__Application New Licence 1-22-02-009 5822403.PDF", "13th day of MARCH, 1967:", "13/03/1967", 7, 0, 1)]
    [InlineData("12303142__Application - Formal Variation - Issued Licence 27.07.2016 9431557.pdf", "27 July 2016", "27/07/2016", 14, 0, 1)]
    [InlineData("12405035__Permit to Abstract - 1_24_5_35 - Licence Document - 10031966.pdf", "10th day of MARCH, 19 66K", "10/03/1966", 5, 0, 1)]
    [InlineData("12502014__Non-Application Licence Document (20.07.2005).PDF", "i2 0 JUL 2005", "20/07/2005", 13, 0, 1)]
    [InlineData("12502032__Non-Application Licence Document [Licence] (16052000).PDF", "16/5/00", "16/05/2000", 13, 0, 1)]
    [InlineData("12502102__Non-Application Licence Document [Original Licence] (27042001).PDF", "3/7/01", "03/07/2001", 13, 0, 1)]
    [InlineData("12502133__Non-Application Licence Document [Licence] (06051998).PDF", "13.5.98", "13/05/1998", 12, 0, 1)]
    [InlineData("12502141__Application type unknown Licence Issued (08.11.2005).PDF", "8 NOV 2005", "08/11/2005", 14, 0, 1)]
    [InlineData("12504120__Abstraction licence.PDF", "28/4/99", "28/04/1999", 12, 0, 1)]
    [InlineData("12401034__1-24-01-034 6099401.pdf", "28th dey of Hay, 1969", "28/05/1969", 6, 0, 1)]
    [InlineData("12502023__Application type unknown Licence Issued 03.05.1966.pdf", "3rd day of MAY, 19 666", "03/05/1966", 6, 0, 1)]
    [InlineData("22712270__Non-Application Licence Document (29.07.2003).PDF", "299 July'03", "29/07/2003", 14, 0, 1)]
    [InlineData("22709167__Non-Application Licence Document (27.03.1997).PDF", "2.7. MAR.1897", "27/03/1897", 11, 0, 1)]
    [InlineData("12506023__Application type unknown Licence Issued (26.01.2006).PDF", "26 JAN 2050", "26/01/2050", 14, 0, 1)] // Should be 2000 but impossible to tell in file, so fine
    [InlineData("22712298__Non-Application Licence Document (27.03.1991).PDF", "2715 day of Marl 1991", "27/03/1991", 5, 0, 1)]
    [InlineData("22709141__Non-Application Licence Document (09.08.1990).PDF", "9Th day of August 1990", "09/08/1990", 4, 0, 1)]
    [InlineData("12304001__1-23-04-001 Licence Issued - 07031966.PDF", "7th day of MARCH .19 66", "07/03/1966", 5, 0, 1)]
    //12504178R01__Application type unknown Licence Issued (01.05.2007).pdf, "299 July'03", // Stamp is incredibly faint, Tesseract doesnt read - Azure AI reads it wrong
    //22630110__Issued licence- 2-26-30-110 6075592.PDF, "299 July'03" // Skips word 'issue' in Azure AI frustratingly
    //12201021__Application New Licence Issued - [1966] - (1966).pdf, "28th day of July 1966" // Doesn't read JULY frustratingly
    public async Task When1_ThenIssueDateCorrectly(
        string filename,
        string expectedIssueDate,
        string expectedIssueDate2,
        int expectedResults,
        int expectedLinkedLicences,
        int expectedLicenceGroups)
    {
        // Act

        
        var resultFull = await GetMatchesAsync(filename, 3);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(expectedResults, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.Equal(expectedIssueDate, dateOfIssue.Text!.First().Text);
        
        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, TestConfig.PdfFolder3),
            AbsLicCacheService);

        var licence = agreedSchemaLicenceGroup[0].Licences[0];

        Assert.NotNull(licence.LicenceVersion.IssueDate);
        Assert.Equal(expectedIssueDate2, licence.LicenceVersion.IssueDate!.Value.ToShortDateString());
        
        Assert.Equal(expectedLicenceGroups, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(expectedLinkedLicences, agreedSchemaLicence.LinkedLicences.Length);
    }
    
    [Theory]
    [InlineData("22702013__2-27-02-013 6999981.PDF", "16 June 2000", "16/06/2000", 13, 0, "2/27/02/013")] // Correct
    [InlineData("22632370__2-26-32-370 6937616.PDF", "9 February 2004", "09/02/2004", 14, 1, "2/26/32/370")] // Correct
    [InlineData("22706035__2-27-06-035 6957806.PDF", "9 FEBRUARY 2004", "09/02/2004", 14, 0, "2/27/06/035")] // Correct
    [InlineData("22707039__Application New Licence Issued - [21.01.2008] - (21.01.2008).PDF", "0 1 OCT 2002", "01/10/2002", 13, 0, "2/27/07/039")] // Correct // TOOD - Fix a bug where it thinks a linked licence number when its actually a 1 and a slash mixed up
    [InlineData("12506023__Application type unknown Licence Issued (26.01.2006).PDF", "26 JAN 2050", "26/01/2050", 14, 0, "1/25/06/023")] // Year incorrect - faint stamp, can't even read as a human
    [InlineData("22634080__Non-Application Licence Document (27.03.1997).PDF", "27 MAR 1997", "27/03/1997", 11, 0, "2/26/34/080")] // Correct
    [InlineData("22709167__Non-Application Licence Document (27.03.1997).PDF", "2.7. MAR.1897", "27/03/1897", 11, 0, "2/27/09/167")] // Incorrect - stamp is not amazing
    [InlineData("22715238__Non-Application Licence Document (05.03.2004).PDF", "5 MAR 2004", "05/03/2004", 14, 0, "2/27/15/238")] // Correct (I think - there is '-' in the stamp)
    public async Task WhenHarishSpottedNoIssueDateFiles1_ThenIssueDateCorrectly(
        string filename,
        string expectedIssueDate,
        string expectedIssueDate2,
        int expectedResults,
        int expectedLinkedLicenceCount,
        string? expectedLicenceNumber)
    {
        // Act

        
        var resultFull = await GetMatchesAsync(filename, 3);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(expectedResults, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.Equal(expectedIssueDate, dateOfIssue.Text!.First().Text);
        
        var schemaData = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, TestConfig.PdfFolder3),
            AbsLicCacheService);

        var licence = schemaData[0].Licences[0];

        Assert.NotNull(licence.LicenceVersion.IssueDate);
        Assert.Equal(expectedIssueDate2, licence.LicenceVersion.IssueDate!.Value.ToShortDateString());
        
        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.NotNull(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(expectedLicenceNumber, agreedSchemaLicence.LicenceNumber?.Value);
        
        Assert.Equal(expectedLinkedLicenceCount, agreedSchemaLicence.LinkedLicences.Length);
    }
    
    [Fact(Skip = "ProblemsWithCarbonPaper")]
    public async Task GetSomeFromTesseractAndSomeFromAzureAi_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Non-Application Licence Document (08.06.1987).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("9th day of January, 1967", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("H.H. Henderson & C. Wentworth-Stanley", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["Succession to licence", "as amended by"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.OnOrNearPreviousLine, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        Assert.Single(abstractionLimitsResult!.SubResults!);

        var abstractionPoint1 = abstractionLimitsResult!.SubResults![0];
        Assert.NotNull(abstractionPoint1);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);
        
        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(2, section1Sub1.SubResults.Count);
        // TODO fix for this
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("5183", perDayValue?.Text?.FirstOrDefault()?.Text); // Should be 5600, bad OCR

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1500", perHourValue?.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/271", licenceNumberResult.Text?.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task WhenZ_B()
    {
        // Arrange

        const string filename = "22630082__Application - New - Issued Licence 12.12.08 10739186.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3);
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("22630082-LV20081212", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/26/30/082", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenZ_L()
    {
        // Arrange

        const string filename = "22728008__2-27-28-008 6846495.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("22728008-LV20070501", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/27/28/008", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal("2/27/28/008", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenZ_ZA()
    {
        // Arrange

        const string filename = "12202087__Non-Application Licence Document [Original Licence] (26112001).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3);
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("12202087-LV20011126", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("1/22/02/087", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal("1/22/02/087", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task Template_Test1()
    {
        // Arrange

        const string fileName = "22712213__Non-Application Licence Document (16.05.1984).PDF";

        // Act
        var resultFull = await _pdfDataExtractor.GetMatchesAsync(
            TestConfig.PdfFolder3 + fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            new LookupConfiguration(
                GetYorkshireLabels(),
                (await LookupConfigurationAsync(3, TestConfig.PdfFolder3)).ValidLowercaseFirstNames,
            new LocalFileService(TestConfig.PdfFolder3),
            CacheService,
            OutputService,
            await firstNamesFixture.GetLicenceNumbersServiceAsync(3, DatabaseCacheService),
            3,
            DateTime.Now),
            [TestConfig.PdfFolder3 + fileName],
            0);

        Assert.Single(resultFull.Item!.Matches!);
    }
    
    [Fact]
    public async Task A3_B466_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "83743S0057__8-37-43-S-0057Plans.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Empty(resultList);  
    }
    
    [Fact]
    public async Task AAA3_B4_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "12203045__Non-Application Licence Document [Original licence] (23051967).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 4);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("NORTHUMBRIAN RIVER AUTHORITY", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.Equal("2 3rd day of MAY, 19 66", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, licenceNumberResult.MatchedLabel!.Position);        
        Assert.Equal("1/22/3/45", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, TestConfig.PdfFolder4),
            AbsLicCacheService);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Equal("12203045-LV19660523", agreedSchemaLicenceGroup[0].LicenceSetId);
        Assert.Equal("045", agreedSchemaLicenceGroup[0].ShortLicenceSetId);
        
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
        Assert.Equal(new DateTime(1966, 05, 23), agreedSchemaLicence.LicenceVersion.IssueDate);
    }
    
    private static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetYorkshireLabels()
    {
        return
        [
            ("YorkshireRiverGroup", GetYorkshireRiverLabels()),
        ];
    }
    
    private static List<LabelToMatch> GetYorkshireRiverLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "YorkshireRiver",
                Format = "Text",
                Text =
                [
                    new(string.Empty)
                    {
                        Regex = YorkshireRiverAuthorityRegex()
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true,
            }
        ];
    }
    
    [GeneratedRegex(".*Yorkshire.* River.* Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex YorkshireRiverAuthorityRegex();
}