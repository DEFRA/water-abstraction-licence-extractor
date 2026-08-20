using FakeItEasy;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.DocumentType.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Services;
using WRADI.Services.AbstractionLicence.Tests.Helper;
using WRADI.Services.Cache.AbstractionLicence;

namespace WRADI.Services.AbstractionLicence.Tests.IntegrationTests.RealNaldData;

public class RealNaldDataPdfPigNoOcrPdfTests1
{
    static RealNaldDataPdfPigNoOcrPdfTests1()
    {
        var realCacheService = new FileSystemCacheService("Cache/");
        var realAbsLicCacheService = new DatabaseAbstractionLicenceCacheService(ReadService, null!);
        
        (CacheService, AbsLicCacheService) = GeneralTestsHelper.GetFakeCacheService(
            realCacheService,
            realAbsLicCacheService,
            [],
            []);

        AbsLicCacheService = realAbsLicCacheService;
        NaldDataLookupService = new NaldDataLookupService(AbsLicCacheService);
    }
    
    private static readonly ICacheService CacheService;
    private static readonly IAbstractionLicenceCacheService AbsLicCacheService;
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
    public async Task WhenX_NotCheckingAbstractionLimits_ThenFoundCorrectly_IncludesAgreedSchema()
    {
        // Arrange

        const string filename = "Application –Transfer– Issued Licence –05072022.pdf";
        const int regionCode = 3;
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1, regionCode);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(16, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder5);
        
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
        Assert.Equal("1/25/04/059", licence.LicenceNumber!.Value);
        
        Assert.NotNull(licence.Purposes);
        Assert.Equal(2, licence.Purposes.Length);
        Assert.NotNull(licence.Purposes[0].ContainedIn);
        Assert.Equal(2, licence.Purposes[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[0].ContainedIn![1].Source);
        Assert.Equal("4.1", licence.Purposes[0].DocumentId);
        Assert.Equal("Private Water Supply", licence.Purposes[0].DocumentDescription);
        Assert.Equal("10081510", licence.Purposes[0].NaldId);
        Assert.Equal("Private Water Supply | Drinking, Cooking, Sanitary, Washing, (Small Garden) - Household", licence.Purposes[0].NaldDescription);

        Assert.Equal(2, licence.Purposes[1].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[1].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[1].ContainedIn![1].Source);
        Assert.Equal("4.2", licence.Purposes[1].DocumentId);
        Assert.Equal("Agriculture (other than Spray Irrigation)", licence.Purposes[1].DocumentDescription);
        Assert.Equal("10080708", licence.Purposes[1].NaldId);
        Assert.Equal("Private Water Undertaking | General Farming & Domestic", licence.Purposes[1].NaldDescription); 
    }
}