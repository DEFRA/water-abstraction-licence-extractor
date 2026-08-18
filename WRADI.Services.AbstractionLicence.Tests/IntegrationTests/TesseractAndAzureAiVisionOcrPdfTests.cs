using FakeItEasy;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
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
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
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
            2 => TestConfig.PdfFolder2,            
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
    
    private static async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode, string pdfFolder)
    {
        var baseFixture = new BaseFixture();
        
        return new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            await CompanyNameHelper.GetFirstNamesCsvFromFileAsync(),
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            await baseFixture.GetLicenceNumbersServiceAsync((short)regionCode, DatabaseCacheService),
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

        Assert.NotNull(licence.Points);
        Assert.Single(licence.Points);
        Assert.Equal("SE 3266 8147", licence.Points[0].GridRef);
        Assert.Equal("A", licence.Points[0].Name);
        Assert.Equal("At National Grid Reference SE 3266 8147", licence.Points[0].Description);
        Assert.Equal("A", licence.Points[0].Id);

        Assert.NotNull(licence.Purposes);
        Assert.Equal(2, licence.Purposes.Length);
        Assert.Equal("(1)", licence.Purposes[0].Id);
        Assert.Equal("Spray Irrigation", licence.Purposes[0].Description);
        Assert.Equal("(2)", licence.Purposes[1].Id);
        Assert.Equal("Agriculture (other than spray Irrigation)", licence.Purposes[1].Description);

        Assert.NotNull(licence.PeriodsOfAbstraction);
        Assert.Equal(2, licence.PeriodsOfAbstraction.Length);
        Assert.Equal("(1)", licence.PeriodsOfAbstraction[0].Id);
        Assert.Equal("During the months of April to September, Inclusive", licence.PeriodsOfAbstraction[0].Description);
        Assert.Equal("(2)", licence.PeriodsOfAbstraction[1].Id);
        Assert.Equal("All year", licence.PeriodsOfAbstraction[1].Description);

        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Equal(2, licence.AbstractionLimits.Individual.Length);

        Assert.Single(licence.AbstractionLimits.Individual[0].Limits);
        Assert.Equal("thousand cubic metres", licence.AbstractionLimits.Individual[0].Limits[0].Units);
        Assert.Equal(41.360, licence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[0].Limits[0].PeriodType);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[0].Points!);
        Assert.Single(licence.AbstractionLimits.Individual[0].Points!);
        Assert.Equal("A", licence.AbstractionLimits.Individual[0].Points![0].Id);
        Assert.True(licence.AbstractionLimits.Individual[0].Points![0].IsImplicit);
        Assert.Single(licence.AbstractionLimits.Individual[0].Purposes!);
        Assert.Equal("(1)", licence.AbstractionLimits.Individual[0].Purposes![0].Id);
        Assert.False(licence.AbstractionLimits.Individual[0].Purposes![0].IsImplicit);
        
        Assert.Single(licence.AbstractionLimits.Individual[1].Limits);
        Assert.Equal("thousand cubic metres", licence.AbstractionLimits.Individual[1].Limits[0].Units);
        Assert.Equal(1, licence.AbstractionLimits.Individual[1].Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[1].Limits[0].PeriodType);
        Assert.Null(licence.AbstractionLimits.Individual[1].Limits[0].Points!);
        Assert.Single(licence.AbstractionLimits.Individual[1].Points!);
        Assert.Equal("A", licence.AbstractionLimits.Individual[1].Points![0].Id);
        Assert.True(licence.AbstractionLimits.Individual[1].Points![0].IsImplicit);
        Assert.Single(licence.AbstractionLimits.Individual[1].Purposes!);
        Assert.Equal("(2)", licence.AbstractionLimits.Individual[1].Purposes![0].Id);
        Assert.False(licence.AbstractionLimits.Individual[1].Purposes![0].IsImplicit);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Single(licence.AbstractionLimits.Aggregates);
        
        Assert.Equal("cubic metres", licence.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(36.36, licence.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerHour, licence.AbstractionLimits.Aggregates[0].Limits[0].PeriodType);
        Assert.Null(licence.AbstractionLimits.Aggregates[0].Limits[0].Points!);
        Assert.Single(licence.AbstractionLimits.Aggregates[0].Points!);
        Assert.Equal("A", licence.AbstractionLimits.Aggregates[0].Points![0].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Points![0].IsImplicit);
        Assert.Equal(2, licence.AbstractionLimits.Aggregates[0].Purposes!.Length);
        Assert.Equal("(1)", licence.AbstractionLimits.Aggregates[0].Purposes![0].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Purposes![0].IsImplicit);
        Assert.Equal("(2)", licence.AbstractionLimits.Aggregates[0].Purposes![1].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Purposes![1].IsImplicit);
        
        Assert.Equal("cubic metres", licence.AbstractionLimits.Aggregates[0].Limits[1].Units);
        Assert.Equal(618.20, licence.AbstractionLimits.Aggregates[0].Limits[1].Value);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Aggregates[0].Limits[1].PeriodType);
        Assert.Null(licence.AbstractionLimits.Aggregates[0].Limits[1].Points!);
        Assert.Single(licence.AbstractionLimits.Aggregates[0].Points!);
        Assert.Equal("A", licence.AbstractionLimits.Aggregates[0].Points![0].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Points![0].IsImplicit);
        Assert.Equal(2, licence.AbstractionLimits.Aggregates[0].Purposes!.Length);
        Assert.Equal("(1)", licence.AbstractionLimits.Aggregates[0].Purposes![0].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Purposes![0].IsImplicit);
        Assert.Equal("(2)", licence.AbstractionLimits.Aggregates[0].Purposes![1].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Purposes![1].IsImplicit);
        
        Assert.Equal("litres", licence.AbstractionLimits.Aggregates[0].Limits[2].Units);
        Assert.Equal(10.10, licence.AbstractionLimits.Aggregates[0].Limits[2].Value);
        Assert.Equal(LimitPeriodType.PerSecond, licence.AbstractionLimits.Aggregates[0].Limits[2].PeriodType);
        Assert.Null(licence.AbstractionLimits.Aggregates[0].Limits[2].Points!);
        Assert.Single(licence.AbstractionLimits.Aggregates[0].Points!);
        Assert.Equal("A", licence.AbstractionLimits.Aggregates[0].Points![0].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Points![0].IsImplicit);
        Assert.Equal(2, licence.AbstractionLimits.Aggregates[0].Purposes!.Length);
        Assert.Equal("(1)", licence.AbstractionLimits.Aggregates[0].Purposes![0].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Purposes![0].IsImplicit);
        Assert.Equal("(2)", licence.AbstractionLimits.Aggregates[0].Purposes![1].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Purposes![1].IsImplicit);
    }
    
    [Fact]
    public async Task WhenComplicatedFile2_ThenGetPurposesPointsAbstractionLimitsCorrectly()
    {
        // Arrange
        var regionCode = 3;

        const string filename = "22722395A__Non-Application Licence Document (22.10.2001).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 2, regionCode: regionCode);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(13, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder3);
        
        var abstractionLicence = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService);
        
        Assert.Single(abstractionLicence);
        Assert.Single(abstractionLicence.First().Licences);
        
        var licence =  abstractionLicence.First().Licences[0];
        Assert.Equal("2/27/22/395A", licence.LicenceNumber!.Value);

        Assert.NotNull(licence.Points);
        Assert.Equal(2, licence.Points.Length);
        Assert.Equal("SE 2858 7577", licence.Points[0].GridRef);
        Assert.Equal("A", licence.Points[0].Name);
        Assert.Equal("At National Grid Reference point SE 2858 7577", licence.Points[0].Description);
        Assert.Equal("(1)", licence.Points[0].Id);
        Assert.Equal("SE 2850 7629", licence.Points[1].GridRef);
        Assert.Equal("B", licence.Points[1].Name);
        Assert.Equal("At National Grid Reference point SE 2850 7629", licence.Points[1].Description);
        Assert.Equal("(2)", licence.Points[1].Id);

        Assert.NotNull(licence.Purposes);
        Assert.Equal(2, licence.Purposes.Length);
        Assert.Equal("(a)", licence.Purposes[0].Id);
        Assert.Equal("Private Water Supply", licence.Purposes[0].Description);
        Assert.Equal("(b)", licence.Purposes[1].Id);
        Assert.Equal("Reservoir Storage for subsequent stream compensation", licence.Purposes[1].Description);

        Assert.Null(licence.AbstractionLimits.Individual);
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Equal(4, licence.AbstractionLimits.Aggregates!.Length);

        var agg = licence.AbstractionLimits.Aggregates[0];
        Assert.Equal(3, agg.Limits.Count);
        Assert.Equal("cubic metres", agg.Limits[0].Units);
        Assert.Equal(9.1, agg.Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerHour, agg.Limits[0].PeriodType);
        Assert.Equal("cubic metres", agg.Limits[1].Units);
        Assert.Equal(218, agg.Limits[1].Value);
        Assert.Equal(LimitPeriodType.PerDay, agg.Limits[1].PeriodType);
        Assert.Equal("litres", agg.Limits[2].Units);
        Assert.Equal(2.53, agg.Limits[2].Value);
        Assert.Equal(LimitPeriodType.PerSecond, agg.Limits[2].PeriodType);
        Assert.Null(agg.Limits[0].Points);
        Assert.Single(agg.Points!);
        Assert.Equal("(1)", agg.Points![0].Id);
        Assert.False(agg.Points![0].IsImplicit);
        Assert.Null(agg.Limits[0].Purposes);
        Assert.Equal(2, agg.Purposes!.Length);
        Assert.Equal("(a)", agg.Purposes![0].Id);
        Assert.True(agg.Purposes![0].IsImplicit);
        Assert.Equal("(b)", agg.Purposes![1].Id);
        Assert.True(agg.Purposes![1].IsImplicit);
        
        agg = licence.AbstractionLimits.Aggregates[1];
        Assert.Equal(3, agg.Limits.Count);
        Assert.Equal("cubic metres", agg.Limits[0].Units);
        Assert.Equal(22.7, agg.Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerHour, agg.Limits[0].PeriodType);
        Assert.Equal("cubic metres", agg.Limits[1].Units);
        Assert.Equal(545, agg.Limits[1].Value);
        Assert.Equal(LimitPeriodType.PerDay, agg.Limits[1].PeriodType);
        Assert.Equal("litres", agg.Limits[2].Units);
        Assert.Equal(6.31, agg.Limits[2].Value);
        Assert.Equal(LimitPeriodType.PerSecond, agg.Limits[2].PeriodType);        
        Assert.NotNull(agg.Points!);
        Assert.Null(agg.Limits[0].Points!);
        Assert.Equal("(2)", agg.Points![0].Id);
        Assert.False(agg.Points![0].IsImplicit);
        Assert.Equal(2, agg.Purposes!.Length);
        Assert.Equal("(a)", agg.Purposes![0].Id);
        Assert.True(agg.Purposes![0].IsImplicit);
        Assert.Equal("(b)", agg.Purposes![1].Id);
        Assert.True(agg.Purposes![1].IsImplicit);
        
        agg = licence.AbstractionLimits.Aggregates[2];
        Assert.Single(agg.Limits);
        Assert.Equal("thousand cubic metres", agg.Limits[0].Units);
        Assert.Equal(66, agg.Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerYear, agg.Limits[0].PeriodType);
        Assert.Null(agg.Limits[0].Points!);
        Assert.NotNull(agg.Points!);
        Assert.Equal(2, agg.Points!.Length);
        Assert.Equal("(1)", agg.Points![0].Id);
        Assert.True(agg.Points![0].IsImplicit);
        Assert.Single(agg.Purposes!);
        Assert.Equal("(2)", agg.Points![1].Id);
        Assert.True(agg.Points![1].IsImplicit);
        Assert.Single(agg.Purposes!);        
        Assert.Equal("(a)", agg.Purposes![0].Id);
        Assert.False(agg.Purposes![0].IsImplicit);
        
        agg = licence.AbstractionLimits.Aggregates[3];
        Assert.Single(agg.Limits);
        Assert.Equal("thousand cubic metres", agg.Limits[0].Units);
        Assert.Equal(10, agg.Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerYear, agg.Limits[0].PeriodType);
        Assert.Null(agg.Limits[0].Points!);
        Assert.Equal(2, agg.Points!.Length);
        Assert.Equal("(1)", agg.Points![0].Id);
        Assert.True(agg.Points![0].IsImplicit);
        Assert.Single(agg.Purposes!);
        Assert.Equal("(2)", agg.Points![1].Id);
        Assert.True(agg.Points![1].IsImplicit);
        Assert.Single(agg.Purposes!);        
        Assert.Equal("(b)", agg.Purposes![0].Id);
        Assert.False(agg.Purposes![0].IsImplicit);
    }
    
    [Fact]
    public async Task WhenComplicatedFile3_ThenGetPurposesPointsAbstractionLimitsCorrectly()
    {
        // Arrange
        var regionCode = 3;

        const string filename = "22722395__Licence (PDF) 24.08.2006 7556094.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 4, regionCode: regionCode);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(21, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder4);
        
        var abstractionLicence = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService);
        
        Assert.Equal(2, abstractionLicence.Count);
        Assert.Single(abstractionLicence.First().Licences);
        
        var licence =  abstractionLicence.First().Licences[0];
        Assert.Equal("2/27/22/395", licence.LicenceNumber!.Value);
        
        Assert.Single(licence.LinkedLicences);

        Assert.NotNull(licence.Points);
        Assert.Single(licence.Points);
        Assert.Equal("SE 2865 7639", licence.Points[0].GridRef);
        Assert.Equal("A", licence.Points[0].Name);
        Assert.Equal("At National Grid Reference point SE 2865 7639", licence.Points[0].Description);
        Assert.Equal("A", licence.Points[0].Id);

        Assert.NotNull(licence.Purposes);
        Assert.Single(licence.Purposes);
        Assert.Null(licence.Purposes[0].Id);
        Assert.Equal("Spray irrigation", licence.Purposes[0].Description);

        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Single(licence.AbstractionLimits.Individual!);

        var limitBlock = licence.AbstractionLimits.Individual[0];
        Assert.Equal(4, limitBlock.Limits.Count);
        Assert.Equal("cubic metres", limitBlock.Limits[0].Units);
        Assert.Equal(22.7, limitBlock.Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerHour, limitBlock.Limits[0].PeriodType); 
        Assert.Single(limitBlock.Points!);
        Assert.Null(limitBlock.Limits[0].Points!);
        Assert.Equal("A", limitBlock.Points![0].Id);
        Assert.True(limitBlock.Points![0].IsImplicit);
        Assert.Single(limitBlock.Purposes!);
        Assert.Null(limitBlock.Purposes![0].Id);
        Assert.True(limitBlock.Purposes![0].IsImplicit);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Single(licence.AbstractionLimits.Aggregates!);

        limitBlock = licence.AbstractionLimits.Aggregates[0];
        Assert.Single(limitBlock.Limits);
        Assert.Equal("thousand cubic metres", limitBlock.Limits[0].Units);
        Assert.Equal(120, limitBlock.Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerYear, limitBlock.Limits[0].PeriodType); 
        Assert.Single(limitBlock.Points!);
        Assert.Equal("A", limitBlock.Points![0].Id);
        Assert.True(limitBlock.Points![0].IsImplicit);
        Assert.Single(limitBlock.Purposes!);
        Assert.Null(limitBlock.Purposes![0].Id);
        Assert.False(limitBlock.Purposes![0].IsImplicit);
    }
}