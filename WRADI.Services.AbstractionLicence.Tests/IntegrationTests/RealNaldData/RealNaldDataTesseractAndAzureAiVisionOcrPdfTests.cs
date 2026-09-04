using FakeItEasy;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
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
using WRADI.DocumentType.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Services;
using WRADI.Services.AbstractionLicence.Tests.Helper;
using WRADI.Services.Cache.AbstractionLicence;
using WRADI.Services.Output.AbstractionLicence;

namespace WRADI.Services.AbstractionLicence.Tests.IntegrationTests.RealNaldData;

public class RealNaldDataTesseractAndAzureAiVisionOcrPdfTests
{
    static RealNaldDataTesseractAndAzureAiVisionOcrPdfTests()
    {
        var realCacheService = new FileSystemCacheService("Cache/");
        var realAbsLicCacheService = new DatabaseAbstractionLicenceCacheService(ReadService, null!);
        var realAbsLicOutputService = new DatabaseAbstractionLicenceOutputService(null!, ReadService, null!, null!);
        
        (CacheService, AbsLicCacheService, AbsLicOutputService) = GeneralTestsHelper.GetFakeCacheService(
            realCacheService,
            realAbsLicCacheService,
            realAbsLicOutputService,
            [],
            []);

        AbsLicCacheService = realAbsLicCacheService;
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
    public async Task WhenComplicatedFile_ThenGetPurposesPointsAbstractionLimitsCorrectly()
    {
        // Arrange
        var regionCode = 3;

        const string filename = "22723435__cc941f55-ce5d-dbbf-9126-a69b8382e3ea.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, regionCode: regionCode);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(7, resultList.Count);
        
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
        Assert.Equal("2/27/23/435", licence.LicenceNumber!.Value);

        Assert.NotNull(licence.Points);
        Assert.Single(licence.Points);
        
        Assert.Equal("SE 3286 8147", licence.Points[0].NationalGridReferences[0].ToString());
        Assert.Equal("A", licence.Points[0].Name);
        Assert.Equal("At National Grid Reference SE 3266 8147 marked \"A\" on the map", licence.Points[0].Description);
        Assert.Equal("BOREHOLE - SHERWOOD SANDSTONE - SINDERBY", licence.Points[0].NaldDescription);
        Assert.Equal("10004638", licence.Points[0].NaldId);
        Assert.Equal("A", licence.Points[0].Id);

        Assert.Equal(2, licence.Points[0].ContainedIn.Length);
        Assert.Equal(InformationSource.Document, licence.Points[0].ContainedIn[0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[0].ContainedIn[1].Source);

        Assert.NotNull(licence.Purposes);
        Assert.Equal(2, licence.Purposes.Length);
        Assert.Equal("(1)", licence.Purposes[0].Id);
        Assert.Equal("Spray Irrigation", licence.Purposes[0].Description);
        Assert.Equal("10029939", licence.Purposes[0].NaldIds![0]);
        Assert.Equal("Agriculture", licence.Purposes[0].NaldLevel1Description);
        Assert.Equal("General Agriculture", licence.Purposes[0].NaldLevel2Description);
        Assert.Equal("Spray Irrigation - Direct", licence.Purposes[0].NaldLevel3Description);        
        Assert.Equal("(2)", licence.Purposes[1].Id);
        Assert.Equal("Agriculture (other than spray Irrigation)", licence.Purposes[1].Description);
        Assert.Equal("10029938", licence.Purposes[1].NaldIds![0]);
        Assert.Equal("Agriculture", licence.Purposes[1].NaldLevel1Description);
        Assert.Equal("General Agriculture", licence.Purposes[1].NaldLevel2Description);
        Assert.Equal("General Farming & Domestic", licence.Purposes[1].NaldLevel3Description);
        
        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Equal(2, licence.AbstractionLimits.Individual.Length);

        Assert.Single(licence.AbstractionLimits.Individual[0].Limits);
        Assert.Equal(41.360, licence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[0].Points!);
        Assert.Single(licence.AbstractionLimits.Individual[0].Points!);
        Assert.Equal("A", licence.AbstractionLimits.Individual[0].Points![0].Id);
        Assert.True(licence.AbstractionLimits.Individual[0].Points![0].IsImplicit);
        Assert.NotNull(licence.AbstractionLimits.Individual[0].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[0].ContainedIn![1].Source);
        
        Assert.Single(licence.AbstractionLimits.Individual[1].Limits);
        Assert.Equal(1, licence.AbstractionLimits.Individual[1].Limits[0].Value);
        Assert.Null(licence.AbstractionLimits.Individual[1].Limits[0].Points!);
        Assert.Single(licence.AbstractionLimits.Individual[1].Points!);
        Assert.Equal("A", licence.AbstractionLimits.Individual[1].Points![0].Id);
        Assert.True(licence.AbstractionLimits.Individual[1].Points![0].IsImplicit);
        Assert.NotNull(licence.AbstractionLimits.Individual[1].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[1].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[1].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[1].ContainedIn![1].Source);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Single(licence.AbstractionLimits.Aggregates);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates[0].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Aggregates[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Aggregates[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Aggregates[0].ContainedIn![1].Source);
        
        Assert.Equal(36.36, licence.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Null(licence.AbstractionLimits.Aggregates[0].Limits[0].Points!);
        Assert.Single(licence.AbstractionLimits.Aggregates[0].Points!);
        Assert.Equal("A", licence.AbstractionLimits.Aggregates[0].Points![0].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Points![0].IsImplicit);
        
        Assert.Equal(618.20, licence.AbstractionLimits.Aggregates[0].Limits[1].Value);
        Assert.Null(licence.AbstractionLimits.Aggregates[0].Limits[1].Points!);
        Assert.Single(licence.AbstractionLimits.Aggregates[0].Points!);
        Assert.Equal("A", licence.AbstractionLimits.Aggregates[0].Points![0].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Points![0].IsImplicit);    
        
        Assert.Equal(10.10, licence.AbstractionLimits.Aggregates[0].Limits[2].Value);
        Assert.Null(licence.AbstractionLimits.Aggregates[0].Limits[2].Points!);
        Assert.Single(licence.AbstractionLimits.Aggregates[0].Points!);
        Assert.Equal("A", licence.AbstractionLimits.Aggregates[0].Points![0].Id);
        Assert.True(licence.AbstractionLimits.Aggregates[0].Points![0].IsImplicit);
    }
    
    [Fact]
    public async Task WhenComplicatedFile2_ThenGetPurposesPointsAbstractionLimitsCorrectly()
    {
        // Arrange
        var regionCode = 3;

        const string filename = "22722395A__Non-Application Licence Document (22.10.2001).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, regionCode: regionCode);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(13, resultList.Count);
        
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
        Assert.Equal("2/27/22/395A", licence.LicenceNumber!.Value);

        Assert.NotNull(licence.Points);
        Assert.Equal(2, licence.Points.Length);
        Assert.Equal("SE 2858 7577", licence.Points[0].NationalGridReferences[0].ToString());
        Assert.Equal("A", licence.Points[0].Name);
        Assert.Equal("At National Grid Reference point SE 2858 7577 marked 'A' on the map", licence.Points[0].Description);
        Assert.Equal("(1)", licence.Points[0].Id);
        Assert.Equal(2, licence.Points[0].ContainedIn.Length);
        Assert.Equal(InformationSource.Document, licence.Points[0].ContainedIn[0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[0].ContainedIn[1].Source);
        Assert.Equal("SE 2850 7629", licence.Points[1].NationalGridReferences[0].ToString());
        Assert.Equal("B", licence.Points[1].Name);
        Assert.Equal("At National Grid Reference point SE 2850 7629 marked 'B' on the map", licence.Points[1].Description);
        Assert.Equal("SE 2850 7629", licence.Points[1].NationalGridReferences[0].ToString());
        Assert.Equal("(2)", licence.Points[1].Id);
        Assert.Equal(2, licence.Points[1].ContainedIn.Length);
        Assert.Equal(InformationSource.Document, licence.Points[1].ContainedIn[0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[1].ContainedIn[1].Source);

        Assert.NotNull(licence.Purposes);
        Assert.Equal(2, licence.Purposes.Length);
        Assert.Equal("(a)", licence.Purposes[0].Id);
        Assert.Equal("Private Water Supply", licence.Purposes[0].Description);
        Assert.Equal("10019820", licence.Purposes[0].NaldIds![0]);
        Assert.Equal("Industrial, Commercial And Public Services", licence.Purposes[0].NaldLevel1Description);
        Assert.Equal("Holiday Sites, Camp Sites & Tourist Attractions", licence.Purposes[0].NaldLevel2Description);
        Assert.Equal("General Use Relating To Secondary Category (Medium Loss)", licence.Purposes[0].NaldLevel3Description);
        Assert.Equal("(b)", licence.Purposes[1].Id);
        Assert.Equal("Reservoir Storage for subsequent stream compensation", licence.Purposes[1].Description);
        Assert.Equal("10021258", licence.Purposes[1].NaldIds![0]);
        Assert.Equal("Environmental", licence.Purposes[1].NaldLevel1Description);
        Assert.Equal("Non-Remedial River/Wetland Support", licence.Purposes[1].NaldLevel2Description);
        Assert.Equal("Transfer Between Sources (Pre Water Act 2003)", licence.Purposes[1].NaldLevel3Description);
        
        Assert.Null(licence.AbstractionLimits.Individual);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Equal(4, licence.AbstractionLimits.Aggregates!.Length);

        var agg = licence.AbstractionLimits.Aggregates[0];
        Assert.NotNull(agg.ContainedIn);
        Assert.Equal(2, agg.ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, agg.ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, agg.ContainedIn![1].Source);
        
        Assert.Equal(3, agg.Limits.Count);
        Assert.Equal(9.1, agg.Limits[0].Value);
        Assert.Equal(218, agg.Limits[1].Value);
        Assert.Equal(2.53, agg.Limits[2].Value);
        Assert.Null(agg.Limits[0].Points);
        Assert.Single(agg.Points!);
        Assert.Equal("(1)", agg.Points![0].Id);
        Assert.False(agg.Points![0].IsImplicit);
        
        agg = licence.AbstractionLimits.Aggregates[1];
        Assert.NotNull(agg.ContainedIn);
        Assert.Equal(2, agg.ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, agg.ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, agg.ContainedIn![1].Source);
        
        Assert.Equal(3, agg.Limits.Count);
        Assert.Equal(22.7, agg.Limits[0].Value);
        Assert.Equal(545, agg.Limits[1].Value);
        Assert.Equal(6.31, agg.Limits[2].Value);    
        Assert.NotNull(agg.Points!);
        Assert.Null(agg.Limits[0].Points!);
        Assert.Equal("(2)", agg.Points![0].Id);
        Assert.False(agg.Points![0].IsImplicit);
        
        agg = licence.AbstractionLimits.Aggregates[2];
        Assert.NotNull(agg.ContainedIn);
        Assert.Equal(2, agg.ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, agg.ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, agg.ContainedIn![1].Source);
        
        Assert.Single(agg.Limits);
        Assert.Equal(66, agg.Limits[0].Value);
        Assert.Null(agg.Limits[0].Points!);
        Assert.NotNull(agg.Points!);
        Assert.Equal(2, agg.Points!.Length);
        Assert.Equal("(1)", agg.Points![0].Id);
        Assert.True(agg.Points![0].IsImplicit);
        
        agg = licence.AbstractionLimits.Aggregates[3];
        Assert.NotNull(agg.ContainedIn);
        Assert.Equal(2, agg.ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, agg.ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, agg.ContainedIn![1].Source);
        
        Assert.Single(agg.Limits);
        Assert.Equal(10, agg.Limits[0].Value);
        Assert.Null(agg.Limits[0].Points!);
        Assert.Equal(2, agg.Points!.Length);
        Assert.Equal("(1)", agg.Points![0].Id);
        Assert.True(agg.Points![0].IsImplicit);
    }
    
    [Fact]
    public async Task WhenComplicatedFile3_ThenGetPurposesPointsAbstractionLimitsCorrectly()
    {
        // Arrange
        var regionCode = 3;

        const string filename = "22722395__Licence (PDF) 24.08.2006 7556094.pdf";

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
        Assert.Equal("2/27/22/395", licence.LicenceNumber!.Value);
        
        Assert.Single(licence.LinkedLicences);

        Assert.NotNull(licence.Points);
        Assert.Single(licence.Points);
        Assert.Equal("SE 2865 7639", licence.Points[0].NationalGridReferences![0].ToString());
        Assert.Equal("At National Grid Reference point SE 2865 7639 marked \"A\" on the map", licence.Points[0].Description);
        Assert.Equal("BOREHOLE - MAGNESIAN LIMESTONE - NORTH STAINLEY", licence.Points[0].NaldDescription);
        Assert.Equal("A", licence.Points[0].Name);
        Assert.Equal("A", licence.Points[0].Id);
        Assert.Equal(2, licence.Points[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Points[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[0].ContainedIn![1].Source);
        
        Assert.NotNull(licence.Purposes);
        Assert.Single(licence.Purposes);
        Assert.Null(licence.Purposes[0].Id);
        Assert.Equal("Spray irrigation", licence.Purposes[0].Description);
        Assert.Equal("10030785", licence.Purposes[0].NaldIds![0]);
        Assert.Equal("Agriculture", licence.Purposes[0].NaldLevel1Description);
        Assert.Equal("General Agriculture", licence.Purposes[0].NaldLevel2Description);
        Assert.Equal("Spray Irrigation - Direct", licence.Purposes[0].NaldLevel3Description);
        
        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Single(licence.AbstractionLimits.Individual!);

        var limitBlock = licence.AbstractionLimits.Individual[0];
        
        Assert.Equal(4, limitBlock.Limits.Count);
        Assert.NotNull(limitBlock.ContainedIn);
        Assert.Equal(2, limitBlock.ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, limitBlock.ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, limitBlock.ContainedIn![1].Source);
        Assert.Equal(22.7, limitBlock.Limits[0].Value);
        Assert.Single(limitBlock.Points!);
        Assert.Null(limitBlock.Limits[0].Points!);
        Assert.Equal("A", limitBlock.Points![0].Id);
        Assert.True(limitBlock.Points![0].IsImplicit);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Single(licence.AbstractionLimits.Aggregates!);

        limitBlock = licence.AbstractionLimits.Aggregates[0];
        
        Assert.Single(limitBlock.Limits);
        Assert.NotNull(limitBlock.ContainedIn);
        Assert.Single(limitBlock.ContainedIn!); // TODO why not 2
        Assert.Equal(InformationSource.Document, limitBlock.ContainedIn![0].Source);
        Assert.Equal(120, limitBlock.Limits[0].Value);
        Assert.Single(limitBlock.Points!);
        Assert.Equal("A", limitBlock.Points![0].Id);
        Assert.True(limitBlock.Points![0].IsImplicit);
    }
    
    [Fact]
    public async Task SomeZeroSwappingInLinkedLicences()
    {
        // Arrange
        var regionCode = 3;

        const string filename = "22710112__2-27-10-112 6959593.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, regionCode: regionCode);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(6, resultList.Count);
        
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
        Assert.Equal("2/27/10/112", licence.LicenceNumber!.Value);
        
        Assert.Equal(3, licence.LinkedLicences.Length);
        Assert.Equal("2/27/10/031", licence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("2/27/10/049", licence.LinkedLicences[1].LicenceNumber);
        Assert.Equal("2/27/10/076", licence.LinkedLicences[2].LicenceNumber);
    }
}