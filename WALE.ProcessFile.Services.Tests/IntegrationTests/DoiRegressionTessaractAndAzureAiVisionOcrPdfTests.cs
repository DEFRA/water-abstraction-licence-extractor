using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.ProcessFile.Services.Tests.Helper;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[Collection("First Names 2")]
public class DoiRegressionTessaractAndAzureAiVisionOcrPdfTests(SingletonFirstNamesFixture firstNamesFixture)
{
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider =
        new(TestConfig.PostgresHost,
            TestConfig.PostgresPort,
            TestConfig.PostgresDbName,
            TestConfig.PostgresUsername,
            TestConfig.PostgresPassword);
    
    private static IDatabaseReadService ReadService =>
        new PostgresReadService(NpgsqlDataSourceProvider);

    static DoiRegressionTessaractAndAzureAiVisionOcrPdfTests()
    {
        LicenceNumber.Instance = new LicenceNumber(ReadService);
    }

    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
 
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
        TestConfig.PdfFolder4);

    private readonly Dictionary<string, DmsFileData> _fileLicenceMapping = new();
    private readonly NaldLicenceStatusData _naldLicenceStatusData = new()
    {
        LiveLicences = [],
        DeadLicences = [],
        ImpoundmentLicences = []
    };
    private readonly Dictionary<string, List<NaldData>> _naldData = [];

    private LookupConfiguration LookupConfiguration(int regionCode)
    {
        return new LookupConfiguration(
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            firstNamesFixture.FirstNamesCsv,
            regionCode);
    }
    
    private Task<MatchesResult> GetMatchesAsync(string fileName, int regionCode, int folderNumber = 1)
    {
        string f;
        IPdfDataExtractorService extractor;

        switch (folderNumber)
        {
            case 5:
                f = TestConfig.PdfFolder5;
                extractor = _pdfDataExtractorCombined5;
                break;
            default:
                throw new Exception("Number not known");
        }
        
        return extractor.GetMatchesAsync(
            f + fileName,
            LookupConfiguration(regionCode),
            [f + fileName],
            0);
    }
    
    [Fact]
    public async Task DoiNotFound_12203045()
    {
        // Arrange
        const string filename = "12203045__Non-Application Licence Document [Original licence] (23051966).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("2 3rd day of MAY, 19 66", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined5,
            TestConfig.PdfFolder5,
            0,
            LookupConfiguration(1));
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("1966-05-23", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
    }
    
    [Fact]
    public async Task DoiNotFound_12205044()
    {
        // NOTE - This one worked even with just Tesseract (as long as the IEH removal code runs)
        
        // Arrange
        const string filename = "12205044__Non-Application Licence Document [Original Licence] (14101966).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("14IEH day of OCTOBER, 1966", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined5,
            TestConfig.PdfFolder5,
            0,
            LookupConfiguration(1));
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("1966-10-14", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
    }
    
    [Fact]
    public async Task DoiNotFound_12303008()
    {
        // Arrange
        const string filename = "12303008__Non-Application Licence Document [Original Licence] (11051966).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("11 th day of NAY, 19 66", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined5,
            TestConfig.PdfFolder5,
            0,
            LookupConfiguration(1));
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("1966-05-11", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
    }
    
    [Fact]
    public async Task DoiNotFound_12303075()
    {
        // Arrange
        const string filename = "12303075__Non-Application Licence Document [Original Licence] (08111966).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("8th day of NOVEMBER, 19 66", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined5,
            TestConfig.PdfFolder5,
            0,
            LookupConfiguration(1));
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("1966-11-08", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
    }
    
    [Fact]
    public async Task DoiNotFound_12303076()
    {
        // Arrange
        const string filename = "12303076__Non-Application Licence Document [Original Licence] (08111966).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 5);
        
        // Assert
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("8th day of NOVEMBER, 19 66", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined5,
            TestConfig.PdfFolder5,
            0,
            LookupConfiguration(1));
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("1966-11-08", agreedSchemaLicence.LicenceVersion.IssueDate!.Value.ToString("yyyy-MM-dd"));
    }
}