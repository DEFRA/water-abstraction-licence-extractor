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
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.Services.Cache.AbstractionLicence;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[EnableParallelization]
[Collection("First Names 2")]
public class DoiRegressionTessaractAndAzureAiVisionOcrPdfTests(SingletonFirstNamesFixture firstNamesFixture)
{
    private static readonly ICacheService CacheService;
    private static readonly IAbstractionLicenceCacheService AbsLicCacheService;

    static DoiRegressionTessaractAndAzureAiVisionOcrPdfTests()
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
    
    private Task SetupLicenceNumbersAsync(short regionCode)
    {
        return firstNamesFixture.SetupLicenceNumbersAsync(regionCode, DatabaseCacheService);
    }

    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = A.Fake<IMessageQueueService>(); 
    
    private readonly IPdfDataExtractorService _pdfDataExtractorCombined5 = new PdfDataExtractorService(
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

    private static readonly Dictionary<string, DmsFileData> _fileLicenceMapping = new();
    private readonly NaldLicenceStatusData _naldLicenceStatusData = new()
    {
        LiveLicences = [],
        LapsedLicences = [],
        ExpiredLicences = [],
        RevokedLicences = [],
        ImpoundmentLicences = []
    };
    private static readonly Dictionary<string, List<NaldData>> _naldData = [];

    private async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode, string pdfFolder)
    {
        return new LookupConfiguration(
            WalLabelConfiguration.GetLabels(),
            await firstNamesFixture.FirstNamesCsvTask(),
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            regionCode,
            DateTime.Now);
    }
    
    private async Task<MatchesResult> GetMatchesAsync(string fileName, int regionCode, int folderNumber = 1)
    {
        string f;

        switch (folderNumber)
        {
            case 5:
                f = TestConfig.PdfFolder5;
                break;
            default:
                throw new Exception("Number not known");
        }
        
        return (await _pdfDataExtractorCombined5.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            await LookupConfigurationAsync(regionCode, f),
            [fileName],
            0)).Item!;
    }
    
    [Fact]
    public async Task DoiNotFound_12203045()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12203045__Non-Application Licence Document [Original licence] (23051966).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("2 3rd day of MAY, 19 66", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("1966-05-23", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/22/3/45", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_12205044()
    {
        // NOTE - This one worked even with just Tesseract (as long as the IEH removal code runs)
        
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12205044__Non-Application Licence Document [Original Licence] (14101966).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("14IEH day of OCTOBER, 1966", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("1966-10-14", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.False(agreedSchemaLicence.NoneSchemaData.ContainsKey("scrapedLicenceNumber"));
    }
    
    [Fact]
    public async Task DoiNotFound_12303008()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12303008__Non-Application Licence Document [Original Licence] (11051966).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("11 th day of NAY, 19 66", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("1966-05-11", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.False(agreedSchemaLicence.NoneSchemaData.ContainsKey("scrapedLicenceNumber"));
    }
    
    [Fact]
    public async Task DoiNotFound_12303075()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12303075__Non-Application Licence Document [Original Licence] (08111966).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("8th day of NOVEMBER, 19 66", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("1966-11-08", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/23/3/75", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_12303076()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12303076__Non-Application Licence Document [Original Licence] (08111966).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("8th day of NOVEMBER, 19 66", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("1966-11-08", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/23/3/76", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_12100001()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12100001__Application Minor Variation Issued Licence 17062025 .pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("17 June 2025", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("2025-06-17", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/21/00/001", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_12100004()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12100004__Application - Renewal - Same Terms – Issued licence - November 2014 8621766.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("11 November 2014", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("2014-11-11", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/21/00/004", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_12100010()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12100010__1-21-00-010 5822315.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("28 DAY OF March 1984", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("1984-03-28", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/21/0/10", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_12100023()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12100023__Application - Transfer - Issued licence 22.7.2016 9423969.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("22 July 2016", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("2016-07-22", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/21/00/023", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_12100052()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12100052__Application - New - Issued licence 8677332.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("17 December 2014", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("2014-12-17", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/21/00/052", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_12100063()
    {
        // Arrange
        const string filename = "12100063__Application type unknown Licence Issued - 05031993.pdf";

        // Act
        await SetupLicenceNumbersAsync(1);
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("5th AY OF March 1993", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("1993-03-05", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/21/0/63", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_12100065()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12100065__Application New Licence Issued - [1974] - (1974).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("21st day of March 1974", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("1974-03-21", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/21/0/65", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_2100068()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12100068__Application Normal Variation Licence Issued 17062025.docx.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("17 June 2025", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("2025-06-17", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/21/00/068", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_12100069()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12100069__Application New Licence Issued - [1997] - (1997).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("30th 1997", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();

        Assert.Null(agreedSchemaLicence.LicenceVersion.IssueDate);

        Assert.Null(agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Equal("1/21/0/069", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
    
    [Fact]
    public async Task DoiNotFound_12100071R01()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12100071R01__Application - New - Issued Licence 15-05-2018 10311405.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("11 May 2018", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractorCombined5,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder5),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("2018-05-11", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
        Assert.Equal("1/21/00/071/R01", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
    }
}