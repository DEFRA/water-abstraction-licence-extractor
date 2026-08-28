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
            new DmsLookupService(),
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
        
        Assert.NotNull(licence.Points);
        Assert.Single(licence.Points);
        Assert.NotNull(licence.Points[0].ContainedIn);
        Assert.Equal(2, licence.Points[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Points[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[0].ContainedIn![1].Source);
        Assert.Equal("2.1", licence.Points[0].Id);
        Assert.Equal("Between National Grid References NZ 6008 0569 and NZ 6037 0556 marked 'A' and 'B' on the map", licence.Points[0].Description);
        Assert.Equal("10008265", licence.Points[0].NaldId);
        Assert.Equal("SPRING - SUPERFICIAL DRIFT - INGLEBY GREENHOW", licence.Points[0].NaldDescription);
        
        Assert.NotNull(licence.Purposes);
        Assert.Equal(2, licence.Purposes.Length);
        Assert.NotNull(licence.Purposes[0].ContainedIn);
        Assert.Equal(2, licence.Purposes[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[0].ContainedIn![1].Source);
        Assert.Equal("4.1", licence.Purposes[0].Id);
        Assert.Equal("Private Water Supply", licence.Purposes[0].Description);
        Assert.Equal("10081510", licence.Purposes[0].NaldIds![0]);
        Assert.Equal("Private Water Supply | Drinking, Cooking, Sanitary, Washing, (Small Garden) - Household", licence.Purposes[0].NaldDescription);

        Assert.Equal(2, licence.Purposes[1].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[1].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[1].ContainedIn![1].Source);
        Assert.Equal("4.2", licence.Purposes[1].Id);
        Assert.Equal("Agriculture (other than Spray Irrigation)", licence.Purposes[1].Description);
        Assert.Equal("10080708", licence.Purposes[1].NaldIds![0]);
        Assert.Equal("Private Water Undertaking | " +
            "General Farming & Domestic", licence.Purposes[1].NaldDescription); 
        
        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Single(licence.AbstractionLimits.Individual);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].Limits.Count);
        Assert.Equal(90.91, licence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Individual[0].Limits[0].PeriodType);
        Assert.Equal(33182, licence.AbstractionLimits.Individual[0].Limits[1].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[1].Units);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[0].Limits[1].PeriodType);
        
        Assert.Null(licence.AbstractionLimits.Aggregates);
    }
    
    [Fact]
    public async Task WhenY1()
    {
        // Arrange
        const string filename = "1.3-licence-07.02.2023.pdf";
        const int regionCode = 5;
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1, regionCode);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(19, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder5);
        
        var abstractionLicence = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService,
            NaldDataLookupService);
        
        Assert.Equal(3, abstractionLicence.Count);
        Assert.Single(abstractionLicence.First().Licences);
        
        var licence =  abstractionLicence.First().Licences[0];
        Assert.Equal("SW/047/0051/003", licence.LicenceNumber!.Value);
        
        Assert.NotNull(licence.Points);
        Assert.Single(licence.Points);
        Assert.NotNull(licence.Points[0].ContainedIn);
        Assert.Equal(2, licence.Points[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Points[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[0].ContainedIn![1].Source);
        Assert.Equal("2.1", licence.Points[0].Id);
        Assert.Equal("At National Grid Reference SX 39921 85071 marked 'A' on the maps", licence.Points[0].Description);
        Assert.Equal("10040099", licence.Points[0].NaldId);
        Assert.Equal("RIVER LYD AT LIFTON", licence.Points[0].NaldDescription);
        
        Assert.NotNull(licence.Purposes);
        Assert.Single(licence.Purposes);
        Assert.NotNull(licence.Purposes[0].ContainedIn);
        Assert.Equal(2, licence.Purposes[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[0].ContainedIn![1].Source);
        Assert.Equal("4.1", licence.Purposes[0].Id);
        Assert.Equal("Transfer for the purpose of filling a reservoir for subsequent abstraction for\npublic water supply", licence.Purposes[0].Description);
        Assert.Equal("10082040", licence.Purposes[0].NaldIds![0]);
        Assert.Equal("Water Supply Related | Transfer Between Sources (Post Water Act 2003)", licence.Purposes[0].NaldDescription);
        
        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Single(licence.AbstractionLimits.Individual);
        Assert.NotNull(licence.AbstractionLimits.Individual[0].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[0].ContainedIn![1].Source);
        Assert.Equal(4, licence.AbstractionLimits.Individual[0].Limits.Count);
        Assert.Equal(2_000, licence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerHour, licence.AbstractionLimits.Individual[0].Limits[0].PeriodType);
        Assert.Equal(40_000, licence.AbstractionLimits.Individual[0].Limits[1].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[1].Units);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Individual[0].Limits[1].PeriodType);
        Assert.Equal(6_000_000, licence.AbstractionLimits.Individual[0].Limits[2].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[2].Units);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[0].Limits[2].PeriodType);
        Assert.Equal(556, licence.AbstractionLimits.Individual[0].Limits[3].Value);
        Assert.Equal("litres", licence.AbstractionLimits.Individual[0].Limits[3].Units);
        Assert.Equal(LimitPeriodType.PerSecond, licence.AbstractionLimits.Individual[0].Limits[3].PeriodType);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Single(licence.AbstractionLimits.Aggregates);
        Assert.NotNull(licence.AbstractionLimits.Aggregates[0].LinkedLicences);        
        Assert.Single(licence.AbstractionLimits.Aggregates[0].LinkedLicences!);
        Assert.Equal("15/47/013/S/020", licence.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.NotNull(licence.AbstractionLimits.Aggregates[0].ContainedIn);
        Assert.Single(licence.AbstractionLimits.Aggregates[0].ContainedIn!);
    }
    
    [Fact]
    public async Task WhenY2()
    {
        // Arrange
        const string filename = "5-licence_lobwood_final.pdf";
        const int regionCode = 5;
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1, regionCode);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(23, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder5);
        
        var abstractionLicence = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService,
            NaldDataLookupService);
        
        Assert.Equal(2, abstractionLicence.Count);
        Assert.Single(abstractionLicence.First().Licences);
        
        var licence = abstractionLicence.First().Licences[0];
        Assert.Equal("2/27/19/129/R01", licence.LicenceNumber!.Value);
        
        Assert.NotNull(licence.Points);
        Assert.Single(licence.Points);
        Assert.NotNull(licence.Points[0].ContainedIn);
        Assert.Equal(2, licence.Points[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Points[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[0].ContainedIn![1].Source);
        Assert.Equal("2.1", licence.Points[0].Id);
        Assert.Equal("At National Grid Reference SE 07537 51958 marked 'A' on the map", licence.Points[0].Description);
        Assert.Equal("32021", licence.Points[0].NaldId);
        Assert.Equal("RIVER WHARFE - LOBWOOD", licence.Points[0].NaldDescription);
        
        Assert.NotNull(licence.Purposes);
        Assert.Single(licence.Purposes);
        Assert.NotNull(licence.Purposes[0].ContainedIn);
        Assert.Equal(2, licence.Purposes[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[0].ContainedIn![1].Source);
        Assert.Equal("4.1", licence.Purposes[0].Id);
        Assert.Equal("Public water supply", licence.Purposes[0].Description);
        Assert.Equal("10083975", licence.Purposes[0].NaldIds![0]);
        Assert.Equal("Public Water Supply | Potable Water Supply - Direct", licence.Purposes[0].NaldDescription);
        
        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Equal(3, licence.AbstractionLimits.Individual.Length);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[0].ContainedIn);
        Assert.NotNull(licence.AbstractionLimits.Individual[0].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[0].ContainedIn![1].Source);
        Assert.Equal(4, licence.AbstractionLimits.Individual[0].Limits.Count);
        Assert.Equal(5_060, licence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerHour, licence.AbstractionLimits.Individual[0].Limits[0].PeriodType);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[1].ContainedIn);
        Assert.Equal(93_200, licence.AbstractionLimits.Individual[0].Limits[1].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[1].Units);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Individual[0].Limits[1].PeriodType);
        Assert.Equal(23_742_000, licence.AbstractionLimits.Individual[0].Limits[2].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[2].Units);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[0].Limits[2].PeriodType);
        
        Assert.Null(licence.AbstractionLimits.Individual[1].TimeCutoff);
        Assert.Single(licence.AbstractionLimits.Individual[1].Limits);
        Assert.NotNull(licence.AbstractionLimits.Individual[1].ContainedIn);
        Assert.Single(licence.AbstractionLimits.Individual[1].ContainedIn!);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[1].ContainedIn![0].Source);
        Assert.Null(licence.AbstractionLimits.Individual[1].Limits[0].ContainedIn);
        Assert.Equal(30, licence.AbstractionLimits.Individual[1].Limits[0].Value);
        Assert.Equal("megalitres", licence.AbstractionLimits.Individual[1].Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Individual[1].Limits[0].PeriodType);
        // TOOD these have exta conditions applied + theres tons I dont check in these tests
        
        Assert.Single(licence.AbstractionLimits.Individual[2].Limits);
        Assert.NotNull(licence.AbstractionLimits.Individual[2].ContainedIn);
        Assert.Single(licence.AbstractionLimits.Individual[2].ContainedIn!);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[2].ContainedIn![0].Source);
        Assert.Equal(27_392_000, licence.AbstractionLimits.Individual[2].Limits[0].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[2].Limits[0].Units);
        
        Assert.Null(licence.AbstractionLimits.Aggregates);
    }
    
    [Fact]
    public async Task WhenY3()
    {
        // Arrange

        // NOTE - This file has no abstraction limits
        const string filename = "06_transfer_application_new_licence_issued_2112018_10555534.pdf";
        const int regionCode = 7;
        
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
        
        Assert.Equal(2, abstractionLicence.Count);
        Assert.Single(abstractionLicence.First().Licences);
        
        var licence =  abstractionLicence.First().Licences[0];
        Assert.Equal("TH/039/0028/051", licence.LicenceNumber!.Value);
        
        Assert.NotNull(licence.Points);
        Assert.Equal(2, licence.Points.Length);
        Assert.NotNull(licence.Points[0].ContainedIn);
        Assert.Equal(2, licence.Points[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Points[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[0].ContainedIn![1].Source);
        Assert.Equal("2.1", licence.Points[0].Id);
        Assert.Equal("Within the area marked 'Abstraction area' on the map and not outside the boundary formed by straight lines running between the following National Grid References: TL 19954 08765, TL 20170 08737, TL 20476 08428, TL 20364 08178, TL 19755 07824 and TL 19432 08396", licence.Points[0].Description);
        Assert.Equal("10034108", licence.Points[0].NaldId);
        Assert.Equal("HATFIELD ROAD QUARRY, HATFIELD, NEAR ST. ALBANS, POINT A", licence.Points[0].NaldDescription);
        Assert.NotNull(licence.Points[1].ContainedIn);
        Assert.Single(licence.Points[1].ContainedIn!);
        Assert.Equal(InformationSource.Nald, licence.Points[1].ContainedIn![0].Source); // NALD has a 2nd point
        Assert.Equal("10034109", licence.Points[1].Id); // TODO why document id? should just be ID i guess
        Assert.Equal("HATFIELD ROAD QUARRY, HATFIELD, NEAR ST. ALBANS, POINT B", licence.Points[1].Description); // TODO why document description should just be ID i guess
        //Assert.Equal("10034108", licence.Points[1].NaldId);
        //Assert.Equal("HATFIELD ROAD QUARRY, HATFIELD, NEAR ST. ALBANS, POINT A", licence.Points[1].NaldDescription);
        
        Assert.NotNull(licence.Purposes);
        Assert.Single(licence.Purposes);
        Assert.NotNull(licence.Purposes[0].ContainedIn);
        Assert.Equal(2, licence.Purposes[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[0].ContainedIn![1].Source);
        Assert.Equal("4.1", licence.Purposes[0].Id);
        Assert.Equal("Transfer for the purpose of dewatering", licence.Purposes[0].Description);
        Assert.Equal("10097553", licence.Purposes[0].NaldIds![0]);
        Assert.Equal("Extractive | Dewatering", licence.Purposes[0].NaldDescription);
        
        Assert.Null(licence.AbstractionLimits.Individual);
        Assert.Null(licence.AbstractionLimits.Aggregates);
    }
    
    [Fact]
    public async Task WhenY4()
    {
        // Arrange
        const string filename = "6.5.4_Application_New_Issued_Licence_20.08.2014.pdf";
        const int regionCode = 5;
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1, regionCode);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(20, resultList.Count);
        
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
        Assert.Equal("SW/045/0002/028", licence.LicenceNumber!.Value);
        
        Assert.NotNull(licence.Points);
        Assert.Single(licence.Points);
        Assert.NotNull(licence.Points[0].ContainedIn);
        Assert.Equal(2, licence.Points[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Points[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[0].ContainedIn![1].Source);
        Assert.Equal("2.1", licence.Points[0].Id);
        Assert.Equal("At National Grid Reference SX 95850 89130 marked 'A' on the map", licence.Points[0].Description);
        Assert.Equal("66883", licence.Points[0].NaldId);
        Assert.Equal("TOPSHAM ROAD SPORTS GROUND BOREHOLE", licence.Points[0].NaldDescription);
        
        Assert.NotNull(licence.Purposes);
        Assert.Single(licence.Purposes);
        Assert.NotNull(licence.Purposes[0].ContainedIn);
        Assert.Equal(2, licence.Purposes[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[0].ContainedIn![1].Source);
        Assert.Equal("4.1", licence.Purposes[0].Id);
        Assert.Equal("Spray irrigation", licence.Purposes[0].Description);
        Assert.Equal("10053626", licence.Purposes[0].NaldIds![0]);
        Assert.Equal("Sports Grounds/Facilities | Spray Irrigation - Direct", licence.Purposes[0].NaldDescription);
        
        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Single(licence.AbstractionLimits.Individual);
        Assert.Equal(4, licence.AbstractionLimits.Individual[0].Limits.Count);
        Assert.NotNull(licence.AbstractionLimits.Individual[0].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[0].ContainedIn![1].Source);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[0].ContainedIn);
        Assert.Equal(9, licence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerHour, licence.AbstractionLimits.Individual[0].Limits[0].PeriodType);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[0].ContainedIn);
        Assert.Equal(75, licence.AbstractionLimits.Individual[0].Limits[1].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[1].Units);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Individual[0].Limits[1].PeriodType);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[2].ContainedIn);
        Assert.Equal(10_000, licence.AbstractionLimits.Individual[0].Limits[2].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[2].Units);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[0].Limits[2].PeriodType);
        
        Assert.Null(licence.AbstractionLimits.Aggregates);
    }
    
    [Fact]
    public async Task WhenY5()
    {
        // Arrange
        const string filename = "Abstraction Licence 7310604.pdf";
        const int regionCode = 4;
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1, regionCode);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(19, resultList.Count);
        
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
        Assert.Equal("2/26/32/328", licence.LicenceNumber!.Value);
        
        Assert.NotNull(licence.Points);
        Assert.Single(licence.Points);
        Assert.NotNull(licence.Points[0].ContainedIn);
        Assert.Equal(2, licence.Points[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Points[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[0].ContainedIn![1].Source);
        Assert.Equal("2.1", licence.Points[0].Id);
        Assert.Equal("At National Grid Reference TA 04990 38509 at the point marked \"A\" on the map", licence.Points[0].Description);
        Assert.Equal("10007720", licence.Points[0].NaldId);
        Assert.Equal("BOREHOLE-CHALK-BEVERLEY", licence.Points[0].NaldDescription);
        
        Assert.NotNull(licence.Purposes);
        Assert.Equal(2, licence.Purposes.Length);
        Assert.NotNull(licence.Purposes[0].ContainedIn);
        Assert.Equal(2, licence.Purposes[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[0].ContainedIn![1].Source);
        Assert.Equal("4.1", licence.Purposes[0].Id);
        Assert.Equal("Lake compentation", licence.Purposes[0].Description);
        Assert.Equal("10081442", licence.Purposes[0].NaldIds![0]);
        Assert.Equal("Make-Up Or Top Up Water", licence.Purposes[0].NaldDescription);
        Assert.NotNull(licence.Purposes[1].ContainedIn);
        Assert.Equal(2, licence.Purposes[1].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[1].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[1].ContainedIn![1].Source);
        Assert.Equal("4.2", licence.Purposes[1].Id);
        Assert.Equal("Domestic & Sanitation", licence.Purposes[1].Description);
        Assert.Equal("10081441", licence.Purposes[1].NaldIds![0]);
        Assert.Equal("Holiday Sites, Camp Sites & Tourist Attractions | Drinking, Cooking, Sanitary, Washing, (Small Garden) - Commercial/Industrial/Public Services", licence.Purposes[1].NaldDescription);
        
        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Equal(2, licence.AbstractionLimits.Individual.Length);
        
        // TODO some bug here about it not picking up purposes properly
        Assert.Equal(5, licence.AbstractionLimits.Individual[0].Limits.Count);
        Assert.NotNull(licence.AbstractionLimits.Individual[0].Limits[0].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].Limits[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[0].Limits[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[0].Limits[0].ContainedIn![1].Source);
        Assert.Equal(15, licence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerHour, licence.AbstractionLimits.Individual[0].Limits[0].PeriodType);
        Assert.NotNull(licence.AbstractionLimits.Individual[0].Limits[1].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].Limits[1].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[0].Limits[1].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[0].Limits[1].ContainedIn![1].Source);        
        Assert.Equal(360, licence.AbstractionLimits.Individual[0].Limits[1].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[1].Units);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Individual[0].Limits[1].PeriodType);
        Assert.NotNull(licence.AbstractionLimits.Individual[0].Limits[2].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].Limits[2].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[0].Limits[2].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[0].Limits[2].ContainedIn![1].Source);  
        Assert.Equal(43180, licence.AbstractionLimits.Individual[0].Limits[2].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[2].Units);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[0].Limits[2].PeriodType);
        Assert.NotNull(licence.AbstractionLimits.Individual[0].Limits[3].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].Limits[3].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[0].Limits[3].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[0].Limits[3].ContainedIn![1].Source);  
        Assert.Equal(0.42, licence.AbstractionLimits.Individual[0].Limits[3].Value);
        Assert.Equal("litres", licence.AbstractionLimits.Individual[0].Limits[3].Units);
        Assert.Equal(LimitPeriodType.PerSecond, licence.AbstractionLimits.Individual[0].Limits[3].PeriodType);
        Assert.Equal(2270, licence.AbstractionLimits.Individual[0].Limits[4].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[4].Units);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[0].Limits[4].PeriodType);
        
        // This is only here because the document has an incorrect litres per second value (miscalculated by an order of magnitude)
        Assert.Null(licence.AbstractionLimits.Individual[1].Limits[0].ContainedIn);
        Assert.NotNull(licence.AbstractionLimits.Individual[1].ContainedIn);
        Assert.Single(licence.AbstractionLimits.Individual[1].ContainedIn!);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[1].ContainedIn![0].Source);
        Assert.Single(licence.AbstractionLimits.Individual[1].Limits);
        Assert.Equal(4.17, licence.AbstractionLimits.Individual[1].Limits[0].Value);
        Assert.Equal("litres", licence.AbstractionLimits.Individual[1].Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerSecond, licence.AbstractionLimits.Individual[1].Limits[0].PeriodType);
        
        Assert.Null(licence.AbstractionLimits.Aggregates);
    }
    
    [Fact]
    public async Task WhenY7_SimilarLinkedLicenceNumbersOnlyDifferingByZeroes()
    {
        // Arrange

        const string filename = "NE0270027041R01__Application Renewal of Licence Issued 17062025.pdf";
        const int regionCode = 3;
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 3, regionCode);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(21, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder5);
        
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
        Assert.Equal("NE/027/0027/041/R01", licence.LicenceNumber!.Value);
        
        Assert.NotNull(licence.Points);
        Assert.Single(licence.Points);
        Assert.NotNull(licence.Points[0].ContainedIn);
        Assert.Equal(2, licence.Points[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Points[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[0].ContainedIn![1].Source);
        Assert.Equal("2.1", licence.Points[0].Id);
        Assert.Equal("Between National Grid References SE 93615 79467 and SE 93894 80417 marked 'A' and 'B' on the map", licence.Points[0].Description);
        Assert.Equal("10034542", licence.Points[0].NaldId);
        Assert.Equal("BROMPTON BECK AT YEDLINGHAM, - THE CARRS", licence.Points[0].NaldDescription);

        Assert.NotNull(licence.Purposes);
        Assert.Single(licence.Purposes);
        Assert.NotNull(licence.Purposes[0].ContainedIn);
        Assert.Equal(2, licence.Purposes[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[0].ContainedIn![1].Source);
        Assert.Equal("4.1", licence.Purposes[0].Id);
        Assert.Equal("Spray irrigation", licence.Purposes[0].Description);
        Assert.Single(licence.Purposes[0].NaldIds!);
        Assert.Equal("10094217", licence.Purposes[0].NaldIds![0]);
        Assert.Equal("General Agriculture | Spray Irrigation - Direct", licence.Purposes[0].NaldDescription);
        
        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Single(licence.AbstractionLimits.Individual);
        Assert.Equal(4, licence.AbstractionLimits.Individual[0].Limits.Count);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[0].ContainedIn);
        Assert.NotNull(licence.AbstractionLimits.Individual[0].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[0].ContainedIn![1].Source);
        Assert.Equal(110, licence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerHour, licence.AbstractionLimits.Individual[0].Limits[0].PeriodType);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[1].ContainedIn);
        Assert.Equal(2400, licence.AbstractionLimits.Individual[0].Limits[1].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[1].Units);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Individual[0].Limits[1].PeriodType);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[2].ContainedIn);
        Assert.Equal(45_000, licence.AbstractionLimits.Individual[0].Limits[2].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[2].Units);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[0].Limits[2].PeriodType);
        Assert.Equal(31, licence.AbstractionLimits.Individual[0].Limits[3].Value);
        Assert.Equal("litres", licence.AbstractionLimits.Individual[0].Limits[3].Units);
        Assert.Equal(LimitPeriodType.PerSecond, licence.AbstractionLimits.Individual[0].Limits[3].PeriodType);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Single(licence.AbstractionLimits.Aggregates);
        Assert.NotNull(licence.AbstractionLimits.Aggregates[0].Limits[0]);
        Assert.Equal(4, licence.AbstractionLimits.Aggregates[0].Limits.Count);
        Assert.Equal(2, licence.AbstractionLimits.Aggregates[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Aggregates[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Aggregates[0].ContainedIn![1].Source);      
        Assert.Equal(110, licence.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerHour, licence.AbstractionLimits.Aggregates[0].Limits[0].PeriodType);
        Assert.Null(licence.AbstractionLimits.Aggregates[0].Limits[0].ContainedIn);
        Assert.Equal(2400, licence.AbstractionLimits.Aggregates[0].Limits[1].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Aggregates[0].Limits[1].Units);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Aggregates[0].Limits[1].PeriodType);
        Assert.Null(licence.AbstractionLimits.Aggregates[0].Limits[1].ContainedIn);
        Assert.Equal(45_000, licence.AbstractionLimits.Aggregates[0].Limits[2].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Aggregates[0].Limits[2].Units);
        Assert.Null(licence.AbstractionLimits.Aggregates[0].Limits[2].ContainedIn);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Aggregates[0].Limits[2].PeriodType);
        Assert.Equal(31, licence.AbstractionLimits.Aggregates[0].Limits[3].Value);
        Assert.Equal("litres", licence.AbstractionLimits.Aggregates[0].Limits[3].Units);
        Assert.Equal(LimitPeriodType.PerSecond, licence.AbstractionLimits.Aggregates[0].Limits[3].PeriodType);
        Assert.Null(licence.AbstractionLimits.Aggregates[0].Limits[3].ContainedIn);
    }
    
        [Fact]
    public async Task WhenY6()
    {
        // Arrange

        const string filename = "Application  new  -licence issued  (08072024).pdf";
        const int regionCode = 1;
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1, regionCode);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(17, resultList.Count);
        
        var config = await LookupConfigurationAsync(regionCode, TestConfig.PdfFolder5);
        
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
        Assert.Equal("NE/026/0032/074", licence.LicenceNumber!.Value);
        
        Assert.NotNull(licence.Points);
        Assert.Equal(2, licence.Points.Length);
        Assert.NotNull(licence.Points[0].ContainedIn);
        Assert.Equal(2, licence.Points[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Points[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[0].ContainedIn![1].Source);
        Assert.Equal("2.1", licence.Points[0].Id);
        Assert.Equal("Within the area edged red on the map only, which is also contained within the boundary formed by straight lines running between the following National Grid References: - TA 08011 44263, TA 08425 44263, TA 08425 44044 and TA 08011 44044, known as Pond S", licence.Points[0].Description);
        Assert.Equal("10043134", licence.Points[0].NaldId);
        Assert.Equal("POND S", licence.Points[0].NaldDescription);
        Assert.NotNull(licence.Points[1].ContainedIn);
        Assert.Equal(2, licence.Points[1].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Points[1].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Points[1].ContainedIn![1].Source);
        Assert.Equal("2.2", licence.Points[1].Id);
        Assert.Equal("Within the area edged red on the map only, which is also contained within the boundary formed by straight lines running between the following National Grid References: - TA 08482 43956, TA 08749 43955, TA 08749 43776 and TA 08482 43776, known as Pond T", licence.Points[1].Description);
        Assert.Equal("10043135", licence.Points[1].NaldId);
        Assert.Equal("POND T", licence.Points[1].NaldDescription);
        
        Assert.NotNull(licence.Purposes);
        Assert.Single(licence.Purposes);
        Assert.NotNull(licence.Purposes[0].ContainedIn);
        Assert.Equal(2, licence.Purposes[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.Purposes[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.Purposes[0].ContainedIn![1].Source);
        Assert.Equal("4.1", licence.Purposes[0].Id);
        Assert.Equal("Spray irrigation", licence.Purposes[0].Description);
        Assert.Equal(2, licence.Purposes[0].NaldIds!.Length);
        Assert.Equal("10089062", licence.Purposes[0].NaldIds![0]);
        Assert.Equal("10089063", licence.Purposes[0].NaldIds![1]);
        Assert.Equal("General Agriculture | Spray Irrigation - Direct", licence.Purposes[0].NaldDescription);
        
        Assert.NotNull(licence.AbstractionLimits.Individual);
        Assert.Equal(2, licence.AbstractionLimits.Individual.Length);
        Assert.Equal(4, licence.AbstractionLimits.Individual[0].Limits.Count);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[0].ContainedIn);
        Assert.NotNull(licence.AbstractionLimits.Individual[0].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[0].ContainedIn![1].Source);
        Assert.Equal(103, licence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerHour, licence.AbstractionLimits.Individual[0].Limits[0].PeriodType);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[1].ContainedIn);
        Assert.Equal(1363, licence.AbstractionLimits.Individual[0].Limits[1].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[1].Units);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Individual[0].Limits[1].PeriodType);
        Assert.Null(licence.AbstractionLimits.Individual[0].Limits[2].ContainedIn);
        Assert.Equal(103_000, licence.AbstractionLimits.Individual[0].Limits[2].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[0].Limits[2].Units);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[0].Limits[2].PeriodType);
        Assert.Equal(4, licence.AbstractionLimits.Individual[1].Limits.Count);
        Assert.Null(licence.AbstractionLimits.Individual[1].Limits[0].ContainedIn);
        Assert.NotNull(licence.AbstractionLimits.Individual[1].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Individual[1].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Individual[1].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Individual[1].ContainedIn![1].Source);
        Assert.Equal(103, licence.AbstractionLimits.Individual[1].Limits[0].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[1].Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerHour, licence.AbstractionLimits.Individual[1].Limits[0].PeriodType);
        Assert.Null(licence.AbstractionLimits.Individual[1].Limits[1].ContainedIn);
        Assert.Equal(1363, licence.AbstractionLimits.Individual[1].Limits[1].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[1].Limits[1].Units);
        Assert.Equal(LimitPeriodType.PerDay, licence.AbstractionLimits.Individual[1].Limits[1].PeriodType);
        Assert.Null(licence.AbstractionLimits.Individual[1].Limits[2].ContainedIn);
        Assert.NotNull(licence.AbstractionLimits.Individual[1].ContainedIn);
        Assert.Equal(103_000, licence.AbstractionLimits.Individual[1].Limits[2].Value);
        Assert.Equal("cubic metres", licence.AbstractionLimits.Individual[1].Limits[2].Units);
        Assert.Equal(LimitPeriodType.PerYear, licence.AbstractionLimits.Individual[1].Limits[2].PeriodType);
        
        Assert.NotNull(licence.AbstractionLimits.Aggregates);
        Assert.Single(licence.AbstractionLimits.Aggregates);
        Assert.NotNull(licence.AbstractionLimits.Aggregates[0].Limits[0]);
        // TODO bring in aggregates from NALD and this will then work
        Assert.Single(licence.AbstractionLimits.Aggregates[0].Limits);
        Assert.Null(licence.AbstractionLimits.Aggregates[0].Limits[0].ContainedIn);
        Assert.Equal(2, licence.AbstractionLimits.Aggregates[0].ContainedIn!.Length);
        Assert.Equal(InformationSource.Document, licence.AbstractionLimits.Aggregates[0].ContainedIn![0].Source);
        Assert.Equal(InformationSource.Nald, licence.AbstractionLimits.Aggregates[0].ContainedIn![1].Source);        
    }
}