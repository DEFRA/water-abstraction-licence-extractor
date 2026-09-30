using FakeItEasy;
using Microsoft.Extensions.Caching.Memory;
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
        
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        NaldDataLookupService = new NaldDataLookupService(AbsLicCacheService, AbsLicOutputService, memoryCache);
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
            null,
            null,
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

        const string filename = "NE0270023036__Application - New - Issued Licence 03.03.2017 9705232.pdf"; // This file messes up as it seems to remove spaces

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
        Assert.Equal(18, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder);
        
        var abstractionLicence = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService,
            NaldDataLookupService);
        
        Assert.Single(abstractionLicence);
        Assert.Single(abstractionLicence.First().Licences);
        
        var licence =  abstractionLicence.First().Licences[0];
        Assert.Equal("2/27/19/166", licence.LicenceNumber!.Value);

        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Single(licence.AbstractionLimits.Individual);
        
        Assert.Null(licence.AbstractionLimits.Aggregates);
        
        Assert.NotNull(licence.LinkedLicences);
        Assert.Empty(licence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenD_E()
    {
        // Arrange
        var regionCode = 3;

        const string filename = "NE0270024103__Application New Licence Issued - [15.03.2024] - (18.03.2024).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, regionCode: regionCode);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(17, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder);
        
        var abstractionLicence = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService,
            NaldDataLookupService);
        
        Assert.Single(abstractionLicence);
        Assert.Single(abstractionLicence.First().Licences);
        
        var licence =  abstractionLicence.First().Licences[0];
        Assert.Equal("NE/027/0024/103", licence.LicenceNumber!.Value);

        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Equal(2, licence.AbstractionLimits.Individual.Length);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Single(licence.AbstractionLimits.Aggregates);
        Assert.NotNull(licence.AbstractionLimits.Aggregates[0].ContainedIn);
        Assert.Single(licence.AbstractionLimits.Aggregates[0].ContainedIn!);
        Assert.Equal(4, licence.AbstractionLimits.Aggregates[0].ContainedIn![0].PageNumber);
        
        Assert.NotNull(licence.LinkedLicences);
        Assert.Empty(licence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenE_F()
    {
        // Arrange
        var regionCode = 3;

        const string filename = "NE0270024095R01__Application Renewal Licence Issued - 17.12.2024.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, regionCode: regionCode);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(20, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder);
        
        var abstractionLicence = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService,
            NaldDataLookupService);
        
        Assert.Single(abstractionLicence);
        Assert.Single(abstractionLicence.First().Licences);
        
        var licence =  abstractionLicence.First().Licences[0];
        Assert.Equal("NE/027/0024/095/R01", licence.LicenceNumber!.Value);

        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Single(licence.AbstractionLimits.Individual);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Equal(4, licence.AbstractionLimits.Aggregates.Length);
        Assert.Equal("6.2.1", licence.AbstractionLimits.Aggregates[0].DocumentIdentifier);
        Assert.NotNull(licence.AbstractionLimits.Aggregates[0].ContainedIn);
        Assert.Single(licence.AbstractionLimits.Aggregates[0].ContainedIn!);
        Assert.Equal(4, licence.AbstractionLimits.Aggregates[0].ContainedIn![0].PageNumber);
        Assert.Equal("6.2.2", licence.AbstractionLimits.Aggregates[1].DocumentIdentifier);
        Assert.Equal("6.3", licence.AbstractionLimits.Aggregates[2].DocumentIdentifier);
        Assert.Equal("6.4", licence.AbstractionLimits.Aggregates[3].DocumentIdentifier);
        
        Assert.NotNull(licence.LinkedLicences);
        Assert.Empty(licence.LinkedLicences);
        
        
    }
}