using FakeItEasy;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.DocumentType.AbstractionLicence.Formats;
using WRADI.Services.AbstractionLicence.Tests.Helper;
using WRADI.Services.Cache.AbstractionLicence;

namespace WRADI.Services.AbstractionLicence.Tests.IntegrationTests;

public class TesseractAndAzureAiVisionOcrPdfTests
{
    static TesseractAndAzureAiVisionOcrPdfTests()
    {
        var realCacheService = new FileSystemCacheService("Cache/");
        var realAbsLicCacheService = new FileSystemAbstractionLicenceCacheService("Cache/");

        (CacheService, AbsLicCacheService) = GeneralTestsHelper.GetFakeCacheService(
            realCacheService,
            realAbsLicCacheService,
            [],
            []);
    }
    
    private static readonly ICacheService CacheService;
    private static readonly IAbstractionLicenceCacheService AbsLicCacheService;
    
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
    
    private async Task<MatchesResult> GetMatchesAsync(string fileName, int useFolder, int regionCode)
    {
        var folder = useFolder switch
        {
            1 => TestConfig.PdfFolder,
            3 => TestConfig.PdfFolder3,
            4 => TestConfig.PdfFolder4,
            5 => TestConfig.PdfFolder5,
            _ => throw new Exception("Number not known")
        };

        return (await _pdfDataExtractor.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            await LookupConfigurationAsync(regionCode, folder),
            [fileName],
            0)).Item!;
    }
    
    private async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode, string pdfFolder)
    {
        return new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            await CompanyNameHelper.GetFirstNamesCsvFromFileAsync(),
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            new AbstractionLicenceNumber([]),
            regionCode,
            DateTime.Now,
            useLockExclusivity: false);
    }
    
    [Fact]
    public async Task WhenComplicatedFile_ThenGetPurposesPointsAbstractionLimitsCorrectly()
    {
        // Arrange
        var regionCode = 3;

        const string filename = "22723435__cc941f55-ce5d-dbbf-9126-a69b8382e3ea.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 5, regionCode: regionCode);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(7, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder5);
        
        var abstractionLicence = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService);
        
        Assert.Single(abstractionLicence);
        Assert.Single(abstractionLicence.First().Licences);
        
        var licence =  abstractionLicence.First().Licences[0];
        Assert.Equal("2/27/23/435", licence.LicenceNumber!.Value);
        
        Assert.Single(licence.Points);
        Assert.Equal("SE 3266 8147", licence.Points[0].GridRef);
        
        Assert.Equal(2, licence.Purposes.Length);
        Assert.Equal("(1)", licence.Purposes[0].Id);
        Assert.Equal("(2)", licence.Purposes[1].Id);
        
        Assert.Equal(2, licence.PeriodsOfAbstraction.Length);
        Assert.Equal("(1)", licence.PeriodsOfAbstraction[0].Id);
        Assert.Equal("(2)", licence.PeriodsOfAbstraction[1].Id);

        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Equal(3, licence.AbstractionLimits.Individual.Length); //TODO should be 2

        Assert.Equal(3, licence.AbstractionLimits.Individual[0].Limits.Count);

        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[0].Units);
        Assert.Equal(36.36, licence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerHour, licence.AbstractionLimits.Individual[0].Limits[0].PeriodType);
        Assert.Single(licence.AbstractionLimits.Individual[0].Limits[0].Points!);
        Assert.True(licence.AbstractionLimits.Individual[0].Limits[0].Points![0].IsImplicit);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].Limits[0].Purposes!.Length);
        Assert.True(licence.AbstractionLimits.Individual[0].Limits[0].Purposes![0].IsImplicit);
        
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[1].Units);
        Assert.Equal(618.20, licence.AbstractionLimits.Individual[0].Limits[1].Value);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Individual[0].Limits[1].PeriodType);
        Assert.Single(licence.AbstractionLimits.Individual[0].Limits[1].Points!);
        Assert.True(licence.AbstractionLimits.Individual[0].Limits[1].Points![0].IsImplicit);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].Limits[1].Purposes!.Length);
        Assert.True(licence.AbstractionLimits.Individual[0].Limits[1].Purposes![0].IsImplicit);
        
        Assert.Equal("litres", licence.AbstractionLimits.Individual[0].Limits[2].Units);
        Assert.Equal(10.10, licence.AbstractionLimits.Individual[0].Limits[2].Value);
        Assert.Equal(LimitPeriodType.PerSecond, licence.AbstractionLimits.Individual[0].Limits[2].PeriodType);
        Assert.Single(licence.AbstractionLimits.Individual[0].Limits[2].Points!);
        Assert.True(licence.AbstractionLimits.Individual[0].Limits[2].Points![0].IsImplicit);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].Limits[2].Purposes!.Length);
        Assert.True(licence.AbstractionLimits.Individual[0].Limits[2].Purposes![0].IsImplicit);
        
        Assert.Single(licence.AbstractionLimits.Individual[1].Limits);

        Assert.Equal("thousand cubic metres", licence.AbstractionLimits.Individual[1].Limits[0].Units);
        Assert.Equal(41.360, licence.AbstractionLimits.Individual[1].Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[1].Limits[0].PeriodType);
        Assert.Single(licence.AbstractionLimits.Individual[1].Limits[0].Points!);
        Assert.True(licence.AbstractionLimits.Individual[1].Limits[0].Points![0].IsImplicit);
        Assert.Single(licence.AbstractionLimits.Individual[1].Limits[0].Purposes!);
        Assert.False(licence.AbstractionLimits.Individual[1].Limits[0].Purposes![0].IsImplicit);
        
        //Assert.NotNull(licence.AbstractionLimits.Aggregates); //TODO
        //Assert.Single(licence.AbstractionLimits.Aggregates); //TODO should be 2
    }
}