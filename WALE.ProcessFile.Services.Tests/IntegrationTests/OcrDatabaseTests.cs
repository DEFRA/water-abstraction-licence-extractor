using FakeItEasy;
using Meziantou.Xunit;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WALE.ProcessFile.Services.Tests.Helper;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Formats;
using WRADI.DocumentType.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Services;
using WRADI.Services.Cache.AbstractionLicence;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[EnableParallelization]
public class OcrDatabaseTests
{
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider =
        new(TestConfig.PostgresHost,
            TestConfig.PostgresPort,
            TestConfig.PostgresDbName,
            TestConfig.PostgresUsername,
            TestConfig.PostgresPassword,
            maxPoolSize: 10);
    
    private static IDatabaseReadService ReadService =>
        new PostgresReadService(NpgsqlDataSourceProvider);

    private static IDatabaseWriteService WriteService =>
        new PostgresWriteService(NpgsqlDataSourceProvider);
    
    private static readonly ICacheService CacheService = new DatabaseCacheService(
        ReadService,
        WriteService);
    
    private static IAbstractionLicenceDatabaseReadService AbsLicReadService =>
        new PostgresAbstractionLicenceReadService(NpgsqlDataSourceProvider);

    private static IAbstractionLicenceDatabaseWriteService AbsLicWriteService =>
        new PostgresAbstractionLicenceWriteService(NpgsqlDataSourceProvider);
    
    private static readonly IAbstractionLicenceCacheService AbsLicCacheService =
        new DatabaseAbstractionLicenceCacheService(
            AbsLicReadService,
            AbsLicWriteService);
    
    private static readonly INaldDataLookupService NaldDataLookupService;
    private static readonly IOutputService OutputService = new DatabaseOutputService(ReadService, WriteService);
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = A.Fake<IMessageQueueService>(); 
    
    public OcrDatabaseTests()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    static OcrDatabaseTests()
    {
        NaldDataLookupService = new NaldDataLookupService(AbsLicCacheService);
    }
    
    private static async Task<ILicenceNumberService> GetLicenceNumbersAsync(short regionCode)
    {
        var allNaldData = await AbsLicCacheService.GetNaldDataAsync(regionCode, false, 0, int.MaxValue);
        return new AbstractionLicenceNumber(allNaldData.AbstractionAndImpoundmentLicences!, []);
    }
    
    private readonly IPdfDataExtractorService _pdfDataExtractorCombined = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.SparseTextOsd, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.Auto, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
        },
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService,
        MessageQueueService);    
    
    private static string PdfFolder => TestConfig.PdfFolder;
    private readonly Dictionary<string, DmsFileData> _fileLicenceMapping = new() {{"", new DmsFileData()}};

    private async Task<MatchesResult> GetMatchesAsync(string fileName)
    {
        return (await _pdfDataExtractorCombined.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            new LookupConfiguration(
                AbstractionLicenceLabelConfiguration.GetLabels(),
                await CompanyNameHelper.GetFirstNamesCsvFromFileAsync(),
                new LocalFileService(PdfFolder),
                CacheService,
                OutputService,
                await GetLicenceNumbersAsync(3),
                new DmsLookupService(),
                3,
                DateTime.Now),
            [fileName],
            0)).Item!;
    }

    [Fact(Skip = "UsedAsAUtilityOnly")]
    //[Fact]
    public async Task ClearCacheAll()
    {
        await CacheService.ClearCacheAsync();
    }
    
    [Fact(Skip = "NeedsReworkingNowWeUseApi")]
    public async Task Uncached_Then_Changed()
    {
        await GetLicenceNumbersAsync(3);
        
        var filename = "14460030853 licence effective 24.07.2005";
        var someGuid = Guid.NewGuid(); // TODO
        
        await CacheService.ClearCacheAsync(someGuid);

        filename = "14460030853 licence effective 24-07-2005";
        someGuid = Guid.NewGuid(); // TODO
        
        await CacheService.ClearCacheAsync(someGuid);
        
        filename = "14460030853 licence effective 24.07.2005.pdf";
        someGuid = Guid.NewGuid(); // TODO
        
        await CacheService.ClearCacheAsync(someGuid);
        
        await ProcessAsync(filename); // Uncached
        await ProcessAsync(filename); // Cached
    }
    
    private async Task ProcessAsync(string filename)
    {
        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        // Tesseract struggles to read licence number in header and abstraction limits
        // in this document. Azure AI does read them

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(8, records.Text!.Count);
        
        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var licenceNumber = resultList.Single(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("14/46/03/0853", licenceNumber.Text?.FirstOrDefault()?.Text);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr T M C Davey", nameResult.Text?[0]?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        Assert.NotNull(abstractionLimitsResult.SubResults);
        Assert.Single(abstractionLimitsResult.SubResults);
        Assert.Equal(16, abstractionLimitsResult.LabelStartLineNumber);
        
        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal(8, abstractionLimitsSection1.Text!.Count);

        Assert.NotNull(abstractionLimitsSection1.SubResults);
        Assert.Single(abstractionLimitsSection1.SubResults!);

        var section1Sub1 = abstractionLimitsSection1.SubResults![0];
        Assert.Equal(8, section1Sub1.SubResults!.Count);

        var linkedLicences = section1Sub1.SubResults.Where(x => x.MatchedLabel?.Name == "LinkedLicenceNumber");
        Assert.Single(linkedLicences);
        
        var linkedLicenceFilenames = section1Sub1.SubResults.Where(x => x.MatchedLabel?.Name == "LinkedLicenceFilename");
        Assert.Empty(linkedLicenceFilenames);
        
        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("77", perDay);

        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        var perYear1 = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("5116", perYear1);
        
        var perYearUnits1 = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits1);
        
        var perYear2 = section1Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("5116", perYear2);
        
        var perYearUnits2 = section1Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits2);        
        
        // See notes RE licence
    }
}