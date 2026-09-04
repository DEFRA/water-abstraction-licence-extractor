using FakeItEasy;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.DocumentType.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Services;
using WRADI.Services.AbstractionLicence.Tests.Helper;
using WRADI.Services.Cache.AbstractionLicence;
using WRADI.Services.Output.AbstractionLicence;

namespace WRADI.Services.AbstractionLicence.Tests.IntegrationTests;

public class PdfPigNoOcrPdfTests
{
    static PdfPigNoOcrPdfTests()
    {
        var realCacheService = new FileSystemCacheService("Cache/");
        var realAbsLicCacheService = new FileSystemAbstractionLicenceCacheService("Cache/");
        var realAbsLicOutputService = new FileSystemAbstractionLicenceOutputService("Cache/");

        (CacheService, AbsLicCacheService, AbsLicOutputService) = GeneralTestsHelper.GetFakeCacheService(
            realCacheService,
            realAbsLicCacheService,
            realAbsLicOutputService,
            [],
            []);
        
        NaldDataLookupService = new NaldDataLookupService(AbsLicCacheService, AbsLicOutputService);
    }
    
    private static readonly ICacheService CacheService;
    private static readonly IAbstractionLicenceCacheService AbsLicCacheService;
    private static readonly IAbstractionLicenceOutputService AbsLicOutputService;
    private static readonly INaldDataLookupService NaldDataLookupService;
    
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    
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
    
    private async Task<MatchesResult> GetMatchesAsync(string fileName, int regionCode)
    {
        return (await _pdfDataExtractor.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder),
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
            new DmsLookupService(),
            regionCode,
            DateTime.Now,
            useLockExclusivity: false);
    }
    
    [Fact]
    public async Task WhenA_B()
    {
        // Arrange
        var regionCode = 3;

        const string filename = "NE0270023036__Application - New - Issued Licence 03.03.2017 9705232.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, regionCode: regionCode);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(21, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder);
        
        var abstractionLicence = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService,
            NaldDataLookupService);
        
        Assert.Equal(2, abstractionLicence.Count);
        Assert.Single(abstractionLicence.First().Licences);
        
        var licence =  abstractionLicence.First().Licences[0];
        Assert.Equal("NE/027/0023/036", licence.LicenceNumber!.Value);

        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Equal(2, licence.AbstractionLimits.Individual.Length);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Single(licence.AbstractionLimits.Aggregates); // This test is mainly to check we don't get two entries here
    }
    
    [Fact]
    public async Task WhenB_C()
    {
        // Arrange
        var regionCode = 3;

        const string filename = "22719166__Application - Transfer -Application New Licence Issued 26_11_2020 00_00_00 11595640.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, regionCode: regionCode);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(19, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder);
        
        var abstractionLicence = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService,
            NaldDataLookupService);
        
        Assert.Equal(2, abstractionLicence.Count);
        Assert.Single(abstractionLicence.First().Licences);
        
        var licence =  abstractionLicence.First().Licences[0];
        Assert.Equal("2/27/19/166", licence.LicenceNumber!.Value);

        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Single(licence.AbstractionLimits.Individual);
        
        Assert.Null(licence.AbstractionLimits.Aggregates);
        
        Assert.NotNull(licence.LinkedLicences);
        Assert.Single(licence.LinkedLicences); // This test is mainly to check we don't get two entries here
        Assert.Equal("2/27/01/009", licence.LinkedLicences[0].LicenceNumber);
    }
}