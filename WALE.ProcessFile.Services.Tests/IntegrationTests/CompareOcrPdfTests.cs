using FakeItEasy;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.AwsTextract;
using WALE.ProcessFile.Services.AzureAiServicesDocumentIntelligence;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WALE.ProcessFile.Services.Tests.Helper;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

public class CompareOcrPdfTests
{
    static CompareOcrPdfTests()
    {
        var realCacheService = new FileSystemCacheService("Cache/");
        CacheService = GeneralTestsHelper.GetFakeCacheService(realCacheService, [], _fileLicenceMapping);
    }
    
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider =
        new(TestConfig.PostgresHost,
            TestConfig.PostgresPort,
            TestConfig.PostgresDbName,
            TestConfig.PostgresUsername,
            TestConfig.PostgresPassword);
    
    private static IDatabaseReadService ReadService =>
        new PostgresReadService(NpgsqlDataSourceProvider);

    private static readonly ICacheService DatabaseCacheService =
        new DatabaseCacheService(ReadService, null!);
    
    private static async Task SetupLicenceNumbersAsync(short regionCode)
    {
        var allNaldData = await DatabaseCacheService.GetNaldDataAsync(regionCode, false, 0, int.MaxValue);
        LicenceNumber.Instance = new LicenceNumber(allNaldData.AbstractionAndImpoundmentLicences!);
    }

    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = A.Fake<IMessageQueueService>(); 
    
    private readonly IPdfDataExtractorService _tesseractSparseTextOsdPdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(
                TestConfig.TesseractPath,
                PageSegMode.SparseTextOsd,
                CacheService, OutputService,
                TestConfig.DotnetPath,
                TestConfig.TesseractExeName,
                TestConfig.TesseractExeDirectory)
        },
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService,
        MessageQueueService);
    
    private readonly IPdfDataExtractorService _tesseractAutoOsdPdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(
                TestConfig.TesseractPath,
                PageSegMode.Auto,
                CacheService,
                OutputService,
                TestConfig.DotnetPath,
                TestConfig.TesseractExeName,
                TestConfig.TesseractExeDirectory)
        },
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService,
        MessageQueueService);
    
    private readonly IPdfDataExtractorService _awsTextractPdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            AwsTextractOcrDataExtractorService.Instance(
                TestConfig.AwsAccessKey,
                TestConfig.AwsSecretKey,
                CacheService,
                OutputService)
        },
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService,
        MessageQueueService);
    
    private readonly IPdfDataExtractorService _documentIntelligencePdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new AzureAiServicesDocumentIntelligenceOcrDataExtractorService(
                TestConfig.AiServicesEndpoint,
                TestConfig.AiServicesKey,
                CacheService,
                OutputService)
        },
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService,
        MessageQueueService);
    
    private readonly IPdfDataExtractorService _aiVisionPdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
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
    
    private static readonly Dictionary<string, DmsFileData> _fileLicenceMapping = new() { { "", new DmsFileData() } };

    private async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode, string pdfFolder)
    {
        return new LookupConfiguration(
            WalLabelConfiguration.GetLabels(),
            await CompanyNameHelper.GetFirstNamesCsvFromFileAsync(),
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            regionCode,
            DateTime.Now);
    }

    private async Task<MatchesResult> GetMatchesAsync(
        string providerName,
        string fileName,
        int regionCode,
        int folderNumber)
    {
        var pdfFolder = folderNumber == 1 ? TestConfig.PdfFolder : TestConfig.PdfFolder3;
        if (folderNumber == 5) pdfFolder = TestConfig.PdfFolder5;

        var provider = providerName switch
        {
            "TesseractSparseTextOsd" => _tesseractSparseTextOsdPdfDataExtractor,
            "TesseractAutoOsd" => _tesseractAutoOsdPdfDataExtractor,
            "AwsTextract" => _awsTextractPdfDataExtractor,
            "DocumentIntelligence" => _documentIntelligencePdfDataExtractor,
            "AiVision" => _aiVisionPdfDataExtractor,
            _ => throw new Exception("Provider name not recognized")
        };

        return (await provider.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            await LookupConfigurationAsync(regionCode, pdfFolder),
            [fileName],
            0)).Item!;
    }
    
    [Fact]
    public async Task Compare_WhenSameFileWithDifferentProviders1_ThenGetResults()
    {
        // Printed EA file from 2000
        
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "28-39-28-0312 5606418.PDF";

        // Act
        var resultListTesseractSparseTextOsd = (await GetMatchesAsync(
            "TesseractSparseTextOsd",
            filename,
            1,
            1)).Matches!;
        
        var resultListTesseractAutoOsd = (await GetMatchesAsync(
            "TesseractAutoOsd",
            filename,
            1,
            1)).Matches!;
        
        var resultListAwsTextract = (await GetMatchesAsync(
            "AwsTextract",
            filename,
            1,
            1)).Matches!;
        
        var resultListDocumentIntelligence = (await GetMatchesAsync(
            "DocumentIntelligence",
            filename,
            1,
            1)).Matches!;
        
        var resultListAiVision = (await GetMatchesAsync(
            "AiVision",
            filename,
            1,
            1)).Matches!;
        
        // Number of matches

        Assert.Equal(10, GeneralTestsHelper.ExcludeSomeMatches(resultListTesseractSparseTextOsd).Count);
        Assert.Equal(9, GeneralTestsHelper.ExcludeSomeMatches(resultListTesseractAutoOsd).Count);
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultListAwsTextract).Count);
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultListDocumentIntelligence).Count);
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultListAiVision).Count);
        
        // Records

        var recordsTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(recordsTesseractSparseTextOsd);
        Assert.Equal(8, recordsTesseractSparseTextOsd.Text!.Count);
        
        var recordsTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(recordsTesseractAutoOsd);
        Assert.Equal(8, recordsTesseractAutoOsd.Text!.Count);
        
        var recordsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(recordsTextract);
        Assert.Equal(8, recordsTextract.Text!.Count);
        
        var recordsDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(recordsDocumentIntelligence);
        Assert.Equal(8, recordsDocumentIntelligence.Text!.Count);
        
        var recordsAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(recordsAiVision);
        Assert.Equal(8, recordsAiVision.Text!.Count);
        
        // Issuer
        
        var issuerResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultTesseractSparseTextOsd);
        Assert.Equal("Environment Agency", issuerResultTesseractSparseTextOsd.Text?.FirstOrDefault()?.Text);        
        
        var issuerResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultTesseractAutoOsd);
        Assert.Equal("Environment Agency", issuerResultTesseractAutoOsd.Text?.FirstOrDefault()?.Text);      

        var issuerResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultDocumentIntelligence);
        Assert.Equal("Environment Agency", issuerResultDocumentIntelligence.Text?.FirstOrDefault()?.Text);
        
        var issuerResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultAiVision);
        Assert.Equal("Environment Agency", issuerResultAiVision.Text?.FirstOrDefault()?.Text);
        
        var issuerResultTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultTextract);
        Assert.Equal("Environment Agency", issuerResultTextract.Text?.FirstOrDefault()?.Text); 
        
        // Company name
        
        var nameResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultTesseractSparseTextOsd);
        Assert.True(nameResultTesseractSparseTextOsd.IsOcr);
        Assert.Equal("CROXLEY HALL WATERS LIMITED", nameResultTesseractSparseTextOsd.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResultTesseractSparseTextOsd.MatchedLabel?.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResultTesseractSparseTextOsd.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResultTesseractSparseTextOsd.MatchedPosition);
        
        var nameResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultTesseractAutoOsd);
        Assert.True(nameResultTesseractAutoOsd.IsOcr);
        Assert.Equal("CROXLEY HALL WATERS LIMITED", nameResultTesseractAutoOsd.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResultTesseractAutoOsd.MatchedLabel?.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResultTesseractAutoOsd.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResultTesseractAutoOsd.MatchedPosition);
        
        var nameResultTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultTextract);
        Assert.True(nameResultTextract.IsOcr);
        Assert.Equal("CROXLEY HALL WATERS LIMITED", nameResultTextract.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResultTextract.MatchedLabel?.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResultTextract.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResultTextract.MatchedPosition);
        
        var nameResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultDocumentIntelligence);
        Assert.True(nameResultDocumentIntelligence.IsOcr);
        Assert.Equal("CROXLEY HALL WATERS LIMITED", nameResultDocumentIntelligence.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResultDocumentIntelligence.MatchedLabel?.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResultDocumentIntelligence.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResultDocumentIntelligence.MatchedPosition);
        
        var nameResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultAiVision);
        Assert.True(nameResultAiVision.IsOcr);
        Assert.Equal("CROXLEY HALL WATERS LIMITED", nameResultAiVision.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResultAiVision.MatchedLabel?.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResultAiVision.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResultAiVision.MatchedPosition);
        
        // Licence number
        
        var licenceNumberResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResultTesseractSparseTextOsd);
        Assert.True(licenceNumberResultTesseractSparseTextOsd.IsOcr);
        Assert.Equal("28/39/28/312", licenceNumberResultTesseractSparseTextOsd.Text!.FirstOrDefault()?.Text);
        
        var licenceNumberResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResultTesseractAutoOsd);
        Assert.True(licenceNumberResultTesseractAutoOsd.IsOcr);
        Assert.Equal("28/39/28/312", licenceNumberResultTesseractAutoOsd.Text!.FirstOrDefault()?.Text);
        
        var licenceNumberResultTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResultTextract);
        Assert.True(licenceNumberResultTextract.IsOcr);
        Assert.Equal("28/39/28/312", licenceNumberResultTextract.Text!.FirstOrDefault()?.Text);
        
        var licenceNumberResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResultDocumentIntelligence);
        Assert.True(licenceNumberResultDocumentIntelligence.IsOcr);
        Assert.Equal("28/39/28/312", licenceNumberResultDocumentIntelligence.Text!.FirstOrDefault()?.Text);
        
        var licenceNumberResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResultAiVision);
        Assert.True(licenceNumberResultAiVision.IsOcr);
        Assert.Equal("28/39/28/312", licenceNumberResultAiVision.Text!.FirstOrDefault()?.Text);
        
        // Abstraction limits
        
        var abstractionLimitsResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResultTesseractSparseTextOsd);
        Assert.True(abstractionLimitsResultTesseractSparseTextOsd.IsOcr);
        Assert.Equal(6, abstractionLimitsResultTesseractSparseTextOsd.Text?.Count);
        
        var abstractionLimitsResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResultTesseractAutoOsd);
        Assert.True(abstractionLimitsResultTesseractAutoOsd.IsOcr);
        Assert.Equal(6, abstractionLimitsResultTesseractAutoOsd.Text?.Count);
        
        var abstractionLimitsResultTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResultTextract);
        Assert.True(abstractionLimitsResultTextract.IsOcr);
        Assert.Equal(6, abstractionLimitsResultTextract.Text?.Count);
        
        var abstractionLimitsResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResultDocumentIntelligence);
        Assert.True(abstractionLimitsResultDocumentIntelligence.IsOcr);
        Assert.Equal(6, abstractionLimitsResultDocumentIntelligence.Text?.Count);
        
        var abstractionLimitsResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResultAiVision);
        Assert.True(abstractionLimitsResultAiVision.IsOcr);
        Assert.Equal(6, abstractionLimitsResultAiVision.Text?.Count);
    }
    
    [Fact]
    public async Task Compare_WhenSameFileWithDifferentProviders2_ThenGetResults()
    {
        // Mersey and River Authority
        
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Old 6078947.PDF";

        // Act
        var resultListTesseractSparseTextOsd = (await GetMatchesAsync(
            "TesseractSparseTextOsd",
            filename,
            1,
            1)).Matches!;
        
        var resultListTesseractAutoOsd = (await GetMatchesAsync(
            "TesseractAutoOsd",
            filename,
            1,
            1)).Matches!;
        
        var resultListAwsTextract = (await GetMatchesAsync(
            "AwsTextract",
            filename,
            1,
            1)).Matches!;
        
        var resultListDocumentIntelligence = (await GetMatchesAsync(
            "DocumentIntelligence",
            filename,
            1,
            1)).Matches!;
        
        var resultListAiVision = (await GetMatchesAsync(
            "AiVision",
            filename,
            1,
            1)).Matches!;
        
        // Assert
        Assert.Equal(5, GeneralTestsHelper.ExcludeSomeMatches(resultListTesseractSparseTextOsd).Count);
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultListTesseractAutoOsd).Count);
        Assert.Equal(10, GeneralTestsHelper.ExcludeSomeMatches(resultListAwsTextract).Count);
        Assert.Equal(9, GeneralTestsHelper.ExcludeSomeMatches(resultListAiVision).Count);
        Assert.Equal(9, GeneralTestsHelper.ExcludeSomeMatches(resultListDocumentIntelligence).Count);
        
        // Licence numbers
        
        var licenceNumberTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberTesseractSparseTextOsd);
        
        var licenceNumberTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberTesseractAutoOsd);
        
        var licenceNumberAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumberAwsTextract);
        Assert.Equal("25/68/1/159", licenceNumberAwsTextract.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumberDocumentIntelligence);
        Assert.Equal("25/68/1/1.59", licenceNumberDocumentIntelligence.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumberAiVision);
        Assert.Equal("25/68/1/159", licenceNumberAiVision.Text?.FirstOrDefault()?.Text);
        
        // Issuer
        
        var issuerResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultTesseractSparseTextOsd);
        Assert.Equal("MERSEY AND WEAVER RIVER AUTHORITY", issuerResultTesseractSparseTextOsd.Text?.FirstOrDefault()?.Text);
        
        var issuerResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultTesseractAutoOsd);
        Assert.Equal("MERSEY AND WEAVER RIVER AUTHORITY", issuerResultTesseractAutoOsd.Text?.FirstOrDefault()?.Text);
        
        var issuerResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultAwsTextract);
        Assert.Equal("MERSEY AND WEAVER RIVER AUTHORITY", issuerResultAwsTextract.Text?.FirstOrDefault()?.Text);
        
        var issuerResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultDocumentIntelligence);
        Assert.Equal("MERSEY AND WEAVER RIVER AUTHORITY", issuerResultDocumentIntelligence.Text?.FirstOrDefault()?.Text);
        
        var issuerResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultAiVision);
        Assert.Equal("MERSEY AND WEAVER RIVER AUTHORITY", issuerResultAiVision.Text?.FirstOrDefault()?.Text);
        
        // Company name
        
        var nameResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResultTesseractSparseTextOsd);
        
        var nameResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultTesseractAutoOsd);
        Assert.True(nameResultTesseractAutoOsd.IsOcr);
        Assert.Equal("MID CHESHIRE WATER BOARD", nameResultTesseractAutoOsd.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultTesseractAutoOsd.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultTesseractAutoOsd.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.FullyOnSameLine, nameResultTesseractAutoOsd.MatchedPosition);
        
        var nameResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultAwsTextract);
        Assert.True(nameResultAwsTextract.IsOcr);
        Assert.Equal("MID CHESHIRE WATER BOARD", nameResultAwsTextract.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultAwsTextract.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultAwsTextract.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.FullyOnSameLine, nameResultAwsTextract.MatchedPosition);
        
        var nameResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResultDocumentIntelligence);
        
        var nameResultAiVision= resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultAiVision);
        Assert.True(nameResultAiVision.IsOcr);
        Assert.Equal("MID CHESHIRE WATER BOARD", nameResultAiVision.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultAiVision.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultAiVision.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.FullyOnSameLine, nameResultAiVision.MatchedPosition);
        
        // Abstraction limits
        
        var abstractionLimitsResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResultTesseractSparseTextOsd);
        Assert.True(abstractionLimitsResultTesseractSparseTextOsd.IsOcr);
        Assert.Equal(12, abstractionLimitsResultTesseractSparseTextOsd.Text?.Count);
        
        var abstractionLimitsResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResultTesseractAutoOsd);
        Assert.True(abstractionLimitsResultTesseractAutoOsd.IsOcr);
        Assert.Equal(8, abstractionLimitsResultTesseractAutoOsd.Text?.Count);
        
        var abstractionLimitsResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResultAwsTextract);
        Assert.True(abstractionLimitsResultAwsTextract.IsOcr);
        Assert.Equal(10, abstractionLimitsResultAwsTextract.Text?.Count);
        
        var abstractionLimitsResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResultDocumentIntelligence);
        Assert.True(abstractionLimitsResultDocumentIntelligence.IsOcr);
        Assert.Equal(10, abstractionLimitsResultDocumentIntelligence.Text?.Count);
        
        var abstractionLimitsResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResultAiVision);
        Assert.True(abstractionLimitsResultAiVision.IsOcr);
        Assert.Equal(9, abstractionLimitsResultAiVision.Text?.Count);
        
        // Linked licence counts
        
        var linkedLicenceNumberCountTesseractSparseTextOsd = resultListTesseractSparseTextOsd.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(12, linkedLicenceNumberCountTesseractSparseTextOsd);
        
        var linkedLicenceNumberCountTesseractAutoOsd = resultListTesseractAutoOsd.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(6, linkedLicenceNumberCountTesseractAutoOsd);
        
        var linkedLicenceNumberCountAwsTextract = resultListAwsTextract.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(7, linkedLicenceNumberCountAwsTextract);

        var linkedLicenceNumberCountDocumentIntelligence = resultListDocumentIntelligence.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(7, linkedLicenceNumberCountDocumentIntelligence);
        
        var linkedLicenceNumberCountAiVision = resultListAiVision.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(7, linkedLicenceNumberCountAiVision);
    }
    
    [Fact]
    public async Task Compare_WhenSameFileWithDifferentProviders3_ThenGetResults()
    {
        // Wessex Water Authority
        
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Non-Application Licence Document (12.09.1979).pdf";

        // Act
        var resultListTesseractSparseTextOsd = (await GetMatchesAsync(
            "TesseractSparseTextOsd",
            filename,
            1,
            1)).Matches!;
        
        var resultListTesseractAutoOsd = (await GetMatchesAsync(
            "TesseractAutoOsd",
            filename,
            1,
            1)).Matches!;
        
        var resultListAwsTextract = (await GetMatchesAsync(
            "AwsTextract",
            filename,
            1,
            1)).Matches!;
        
        var resultListDocumentIntelligence = (await GetMatchesAsync(
            "DocumentIntelligence",
            filename,
            1,
            1)).Matches!;
        
        var resultListAiVision = (await GetMatchesAsync(
            "AiVision",
            filename,
            1,
            1)).Matches!;
        
        // Counts
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultListTesseractSparseTextOsd).Count);
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultListTesseractAutoOsd).Count);
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultListAwsTextract).Count);
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultListDocumentIntelligence).Count);
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultListAiVision).Count);
        
        // Issuer
        var issuerResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultTesseractSparseTextOsd);
        Assert.Equal("WESSEX WATER AUTHORITY", issuerResultTesseractSparseTextOsd.Text?.FirstOrDefault()?.Text);
        
        var issuerResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultTesseractAutoOsd);
        Assert.Equal("WESSEX WATER AUTHORITY", issuerResultTesseractAutoOsd.Text?.FirstOrDefault()?.Text);
        
        var issuerResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultAwsTextract);
        Assert.Equal("WESSEX WATER AUTHORITY", issuerResultAwsTextract.Text?.FirstOrDefault()?.Text);
        
        var issuerResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultDocumentIntelligence);
        Assert.Equal("WESSEX WATER AUTHORITY", issuerResultDocumentIntelligence.Text?.FirstOrDefault()?.Text);
        
        var issuerResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultAiVision);
        Assert.Equal("WESSEX WATER AUTHORITY", issuerResultAiVision.Text?.FirstOrDefault()?.Text);
        
        // Company name
        
        var nameResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultTesseractSparseTextOsd);
        Assert.True(nameResultTesseractSparseTextOsd.IsOcr);
        Assert.Single(nameResultTesseractSparseTextOsd.Text!);
        Assert.Equal("Mr A Roas", nameResultTesseractSparseTextOsd.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultTesseractSparseTextOsd.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultTesseractSparseTextOsd.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultTesseractSparseTextOsd.MatchedPosition);
        
        var nameResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultTesseractAutoOsd);
        Assert.True(nameResultTesseractAutoOsd.IsOcr);
        Assert.Single(nameResultTesseractAutoOsd.Text!);
        Assert.Equal("Mr A Roas", nameResultTesseractAutoOsd.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultTesseractAutoOsd.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultTesseractAutoOsd.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultTesseractAutoOsd.MatchedPosition);
        
        var nameResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultAwsTextract);
        Assert.True(nameResultAwsTextract.IsOcr);
        Assert.Single(nameResultAwsTextract.Text!);
        Assert.Equal("Mr A Ross", nameResultAwsTextract.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultAwsTextract.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultAwsTextract.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultAwsTextract.MatchedPosition);
        
        var nameResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultAiVision);
        Assert.True(nameResultAiVision.IsOcr);
        Assert.Single(nameResultAiVision.Text!);
        Assert.Equal("Mr A Ross", nameResultAiVision.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultAiVision.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultAiVision.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultAiVision.MatchedPosition);
        
        var nameResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultDocumentIntelligence);
        Assert.True(nameResultDocumentIntelligence.IsOcr);
        Assert.Single(nameResultDocumentIntelligence.Text!);
        Assert.Equal("Mr A Ross", nameResultDocumentIntelligence.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultDocumentIntelligence.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultDocumentIntelligence.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultDocumentIntelligence.MatchedPosition);
        
        // Licence numbers
        
        var licenceNumberTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberTesseractSparseTextOsd);
        
        var licenceNumberTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberTesseractAutoOsd);
        
        var licenceNumberAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberAwsTextract);
        
        var licenceNumberDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberDocumentIntelligence);
        
        var licenceNumberAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberAiVision);
        
        // Abstraction limits
        
        var abstractionLimitsResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultTesseractSparseTextOsd);
        
        var abstractionLimitsResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultTesseractAutoOsd);
        
        var abstractionLimitsResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultAwsTextract);
        
        var abstractionLimitsResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultDocumentIntelligence);
        
        var abstractionLimitsResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultAiVision);
        
        // Linked licence counts
        
        var linkedLicenceNumberCountTesseractSparseTextOsd = resultListTesseractSparseTextOsd.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountTesseractSparseTextOsd);
        
        var linkedLicenceNumberCountTesseractAutoOsd = resultListTesseractAutoOsd.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountTesseractAutoOsd);
        
        var linkedLicenceNumberCountAwsTextract = resultListAwsTextract.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountAwsTextract);

        var linkedLicenceNumberCountDocumentIntelligence = resultListDocumentIntelligence.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountDocumentIntelligence);
        
        var linkedLicenceNumberCountAiVision = resultListAiVision.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountAiVision);
    }
    
    [Fact]
    public async Task Compare_WhenSameFileWithDifferentProviders4_ThenGetResults()
    {
        // Wessex Water Authority
        
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "original licence (01.06.1966).PDF";

        // Act
        var resultListTesseractSparseTextOsd = (await GetMatchesAsync(
            "TesseractSparseTextOsd",
            filename,
            1,
            1)).Matches!;
        
        var resultListTesseractAutoOsd = (await GetMatchesAsync(
            "TesseractAutoOsd",
            filename,
            1,
            1)).Matches!;
        
        var resultListAwsTextract = (await GetMatchesAsync(
            "AwsTextract",
            filename,
            1,
            1)).Matches!;
        
        var resultListDocumentIntelligence = (await GetMatchesAsync(
            "DocumentIntelligence",
            filename,
            1,
            1)).Matches!;
        
        var resultListAiVision = (await GetMatchesAsync(
            "AiVision",
            filename,
            1,
            1)).Matches!;
        
        // Counts
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultListTesseractSparseTextOsd).Count);
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultListTesseractAutoOsd).Count);
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultListAwsTextract).Count);
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultListDocumentIntelligence).Count);
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultListAiVision).Count);
        
        // Issuer
        var issuerResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultTesseractSparseTextOsd);
        Assert.Equal("WESSEX WATER AUTHORITY", issuerResultTesseractSparseTextOsd.Text?.FirstOrDefault()?.Text);
        
        var issuerResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultTesseractAutoOsd);
        Assert.Equal("WESSEX WATER AUTHORITY", issuerResultTesseractAutoOsd.Text?.FirstOrDefault()?.Text);
        
        var issuerResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultAwsTextract);
        Assert.Equal("WESSEX WATER AUTHORITY", issuerResultAwsTextract.Text?.FirstOrDefault()?.Text);
        
        var issuerResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultDocumentIntelligence);
        Assert.Equal("WESSEX WATER AUTHORITY", issuerResultDocumentIntelligence.Text?.FirstOrDefault()?.Text);
        
        var issuerResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultAiVision);
        Assert.Equal("WESSEX WATER AUTHORITY", issuerResultAiVision.Text?.FirstOrDefault()?.Text);
        
        // Company name
        
        var nameResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultTesseractSparseTextOsd);
        Assert.True(nameResultTesseractSparseTextOsd.IsOcr);
        Assert.Single(nameResultTesseractSparseTextOsd.Text!);
        Assert.Equal("MARKS BARN FARM (CREWKERNE) LTD", nameResultTesseractSparseTextOsd.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultTesseractSparseTextOsd.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultTesseractSparseTextOsd.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultTesseractSparseTextOsd.MatchedPosition);
        
        var nameResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultTesseractAutoOsd);
        Assert.True(nameResultTesseractAutoOsd.IsOcr);
        Assert.Single(nameResultTesseractAutoOsd.Text!);
        Assert.Equal("MARKS BARN FARM (CREWKERNE) LTD", nameResultTesseractAutoOsd.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultTesseractAutoOsd.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultTesseractAutoOsd.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultTesseractAutoOsd.MatchedPosition);
        
        var nameResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultAwsTextract);
        Assert.True(nameResultAwsTextract.IsOcr);
        Assert.Single(nameResultAwsTextract.Text!);
        Assert.Equal("MARKS BARN FARM ( CREWKERNE) LTD", nameResultAwsTextract.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultAwsTextract.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultAwsTextract.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultAwsTextract.MatchedPosition);
        
        var nameResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultAiVision);
        Assert.True(nameResultAiVision.IsOcr);
        Assert.Single(nameResultAiVision.Text!);
        Assert.Equal("MARKS BARN FARM ( CREWKERNE) LTD", nameResultAiVision.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultAiVision.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultAiVision.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultAiVision.MatchedPosition);
        
        var nameResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResultDocumentIntelligence);
        Assert.True(nameResultDocumentIntelligence.IsOcr);
        Assert.Single(nameResultDocumentIntelligence.Text!);
        Assert.Equal("MARKS BARN FARM ( CREWKERNE) LTD", nameResultDocumentIntelligence.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResultDocumentIntelligence.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultDocumentIntelligence.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultDocumentIntelligence.MatchedPosition);
        
        // Licence numbers
        
        var licenceNumberTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberTesseractSparseTextOsd);
        
        var licenceNumberTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberTesseractAutoOsd);
        
        var licenceNumberAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberAwsTextract);
        
        var licenceNumberDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberDocumentIntelligence);
        
        var licenceNumberAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberAiVision);
        
        // Abstraction limits
        
        var abstractionLimitsResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultTesseractSparseTextOsd);
        
        var abstractionLimitsResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultTesseractAutoOsd);
        
        var abstractionLimitsResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultAwsTextract);
        
        var abstractionLimitsResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultDocumentIntelligence);
        
        var abstractionLimitsResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultAiVision);
        
        // Linked licence counts
        
        var linkedLicenceNumberCountTesseractSparseTextOsd = resultListTesseractSparseTextOsd.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountTesseractSparseTextOsd);
        
        var linkedLicenceNumberCountTesseractAutoOsd = resultListTesseractAutoOsd.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountTesseractAutoOsd);
        
        var linkedLicenceNumberCountAwsTextract = resultListAwsTextract.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountAwsTextract);

        var linkedLicenceNumberCountDocumentIntelligence = resultListDocumentIntelligence.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountDocumentIntelligence);
        
        var linkedLicenceNumberCountAiVision = resultListAiVision.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountAiVision);
    }
    
    [Fact]
    public async Task Compare_WhenSameFileWithDifferentProviders5_ThenGetResults()
    {
        // The Somerset River Authority
        
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "original licence (01.09.1966).PDF";

        // Act
        var resultListTesseractSparseTextOsd = (await GetMatchesAsync(
            "TesseractSparseTextOsd",
            filename,
            1,
            1)).Matches!;
        
        var resultListTesseractAutoOsd = (await GetMatchesAsync(
            "TesseractAutoOsd",
            filename,
            1,
            1)).Matches!;
        
        var resultListAwsTextract = (await GetMatchesAsync(
            "AwsTextract",
            filename,
            1,
            1)).Matches!;
        
        var resultListDocumentIntelligence = (await GetMatchesAsync(
            "DocumentIntelligence",
            filename,
            1,
            1)).Matches!;
        
        var resultListAiVision = (await GetMatchesAsync(
            "AiVision",
            filename,
            1,
            1)).Matches!;
        
        // Counts
        #pragma warning disable xUnit2013
        Assert.Equal(1, GeneralTestsHelper.ExcludeSomeMatches(resultListTesseractSparseTextOsd).Count);
        Assert.Equal(1, GeneralTestsHelper.ExcludeSomeMatches(resultListTesseractAutoOsd).Count);
        Assert.Equal(5, GeneralTestsHelper.ExcludeSomeMatches(resultListAwsTextract).Count);
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultListDocumentIntelligence).Count);
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultListAiVision).Count);
        #pragma warning restore xUnit2013
        
        // Issuer
        var issuerResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultTesseractSparseTextOsd);
        Assert.Equal("THE SOMERSET RIVER AUTHORITY", issuerResultTesseractSparseTextOsd.Text?.FirstOrDefault()?.Text);
        
        var issuerResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultTesseractAutoOsd);
        Assert.Equal("THE SOMERSET RIVER AUTHORITY", issuerResultTesseractAutoOsd.Text?.FirstOrDefault()?.Text);
        
        var issuerResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultAwsTextract);
        Assert.Equal("THE SOMERSET RIVER AUTHORITY", issuerResultAwsTextract.Text?.FirstOrDefault()?.Text);
        
        var issuerResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultDocumentIntelligence);
        Assert.Equal("THE SOMERSET RIVER AUTHORITY", issuerResultDocumentIntelligence.Text?.FirstOrDefault()?.Text);
        
        var issuerResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResultAiVision);
        Assert.Equal("THE SOMERSET RIVER AUTHORITY", issuerResultAiVision.Text?.FirstOrDefault()?.Text);
        
        // Company name
        
        var nameResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResultTesseractSparseTextOsd);
        
        var nameResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResultTesseractAutoOsd);
        
        var nameResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResultAwsTextract);
        Assert.True(nameResultAwsTextract.IsOcr);
        Assert.Single(nameResultAwsTextract.Text!);
        Assert.Equal("WALRONDS PARK LTD", nameResultAwsTextract.Text?.FirstOrDefault()?.Text);
        Assert.Contains("authority hereby licence", nameResultAwsTextract.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultAwsTextract.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultAwsTextract.MatchedPosition);
        
        var nameResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResultAiVision);
        Assert.True(nameResultAiVision.IsOcr);
        Assert.Single(nameResultAiVision.Text!);
        Assert.Equal("WALRONDS PARK LTD", nameResultAiVision.Text?.FirstOrDefault()?.Text);
        Assert.Contains("authority hereby licence", nameResultAiVision.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultAiVision.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultAiVision.MatchedPosition);
        
        var nameResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResultDocumentIntelligence);
        Assert.True(nameResultDocumentIntelligence.IsOcr);
        Assert.Single(nameResultDocumentIntelligence.Text!);
        Assert.Equal("WALRONDS PARK LTD", nameResultDocumentIntelligence.Text?.FirstOrDefault()?.Text);
        Assert.Contains("authority hereby licence", nameResultDocumentIntelligence.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResultDocumentIntelligence.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResultDocumentIntelligence.MatchedPosition);
        
        // Licence numbers
        
        var licenceNumberTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberTesseractSparseTextOsd);
        
        var licenceNumberTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberTesseractAutoOsd);
        
        var licenceNumberAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberAwsTextract);
        
        var licenceNumberDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberDocumentIntelligence);
        
        var licenceNumberAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberAiVision);
        
        // Abstraction limits
        
        var abstractionLimitsResultTesseractSparseTextOsd = resultListTesseractSparseTextOsd.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultTesseractSparseTextOsd);
        
        var abstractionLimitsResultTesseractAutoOsd = resultListTesseractAutoOsd.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultTesseractAutoOsd);
        
        var abstractionLimitsResultAwsTextract = resultListAwsTextract.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResultAwsTextract);
        
        var abstractionLimitsResultDocumentIntelligence = resultListDocumentIntelligence.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResultDocumentIntelligence);
        Assert.True(abstractionLimitsResultDocumentIntelligence.IsOcr);
        Assert.Equal(4, abstractionLimitsResultDocumentIntelligence.Text?.Count);
        
        var abstractionLimitsResultAiVision = resultListAiVision.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResultAiVision);
        Assert.True(abstractionLimitsResultAiVision.IsOcr);
        Assert.Equal(4, abstractionLimitsResultAiVision.Text?.Count);
        
        // Linked licence counts
        
        var linkedLicenceNumberCountTesseractSparseTextOsd = resultListTesseractSparseTextOsd.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountTesseractSparseTextOsd);
        
        var linkedLicenceNumberCountTesseractAutoOsd = resultListTesseractAutoOsd.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountTesseractAutoOsd);
        
        var linkedLicenceNumberCountAwsTextract = resultListAwsTextract.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountAwsTextract);

        var linkedLicenceNumberCountDocumentIntelligence = resultListDocumentIntelligence.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountDocumentIntelligence);
        
        var linkedLicenceNumberCountAiVision = resultListAiVision.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(0, linkedLicenceNumberCountAiVision);
    }
}