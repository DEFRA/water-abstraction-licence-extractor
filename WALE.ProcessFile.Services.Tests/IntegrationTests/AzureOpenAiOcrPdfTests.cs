using FakeItEasy;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.AzureOpenAi;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tests.Helper;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.DocumentType.AbstractionLicence.Formats;
using WRADI.Services.Cache.AbstractionLicence;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

public class AzureOpenAiOcrPdfTests
{
    private static readonly ICacheService CacheService;
    private static readonly IAbstractionLicenceCacheService AbsLicCacheService;

    static AzureOpenAiOcrPdfTests()
    {
        var realCacheService = new FileSystemCacheService("Cache/");
        var realAbsLicCacheService = new FileSystemAbstractionLicenceCacheService("Cache/");

        (CacheService, AbsLicCacheService) = GeneralTestsHelper.GetFakeCacheService(
            realCacheService,
            realAbsLicCacheService,
            [],
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
    
    private static async Task<ILicenceNumberService> GetLicenceNumbersAsync(short regionCode)
    {
        var allNaldData = await DatabaseCacheService.GetNaldDataAsync(regionCode, false, 0, int.MaxValue);
        return new AbstractionLicenceNumber(allNaldData.AbstractionAndImpoundmentLicences!);
    }
    
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = A.Fake<IMessageQueueService>(); 
    
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new AzureOpenAiOcrDataExtractorService(
                TestConfig.OpenAiEndpoint,
                TestConfig.OpenAiKey,
                TestConfig.OpenAiModelName,
                TestConfig.OpenAiDeploymentName,
                CacheService)
        },
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService,
        MessageQueueService);
    
    private static readonly Dictionary<string, DmsFileData> _fileLicenceMapping = new() {{"", new DmsFileData()}};

    private string PdfFolder => TestConfig.PdfFolder;

    private async Task<LookupConfiguration> LookupConfigurationAsync(string pdfFolder)
    {
        return new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            await CompanyNameHelper.GetFirstNamesCsvFromFileAsync(),
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            await GetLicenceNumbersAsync(4),
            4,
            DateTime.Now); // TODO - whatever Hampshire & IOW is
    }
    
    private async Task<MatchesResult> GetMatchesAsync(string fileName)
    {
        return (await _pdfDataExtractor.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            await LookupConfigurationAsync(PdfFolder),
            
            [fileName],
            0)).Item!;
    }
    
    [Fact]
    public async Task Handsigned_WhenNearPreviousLineIsCompany_ThenFoundCorrect_Ish()
    {
        // Arrange

        const string filename = "Non-Application Licence Document (22.09.1986).PDF";
        
        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(8, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count); // Fluctuates between 7 and 8 a bit

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("SOUTHERN WATER AUTHORITY", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("22ND DAY OF SEPTEMBER 1986", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.True(nameResult!.IsOcr);
        
        // NOTE - According to companies house this is actual H.N. BUTLER FARMS LTD        
        Assert.EndsWith(" Ltd", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("(hereinafter referred to as \"the Authority\")", nameResult.MatchedLabel!.Text!.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count);

        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(6, section1Sub1.SubResults!.Count);
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("36000", perDayValue?.Text?.FirstOrDefault()?.Text);

        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1500", perHourValue?.Text?.FirstOrDefault()?.Text);
        
        var inTotalUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "InTotalUnits");
        Assert.Equal("gallons", inTotalUnits?.Text?.FirstOrDefault()?.Text);

        var inTotalValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "InTotalValue");
        Assert.Equal("500000", inTotalValue?.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("11/42/28.2/7", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        // TODO - other 2 things

        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(TestConfig.PdfFolder),
            AbsLicCacheService);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.First().Licences.First();
        Assert.Equal("11/42/28.2/7", agreedSchemaLicence.LicenceNumber?.Value);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("11/42/28.2/49", agreedSchemaLicence.LinkedLicences[0].LicenceNumber); // TODO should be this
    }
}