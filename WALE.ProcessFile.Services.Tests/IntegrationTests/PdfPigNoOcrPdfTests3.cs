using Meziantou.Xunit;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tests.Helper;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.Core.AbstractionLicence.Interfaces;
using FakeItEasy;
using WALE.ProcessFile.Core.Models.Dms;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.DocumentType.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Services;
using WRADI.Services.Cache.AbstractionLicence;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[Collection("PdfPigNoOcrPdfTests3 Collection")]
[EnableParallelization]
public class PdfPigNoOcrPdfTests3(StandaloneFixture3 fixture)
{
    private static readonly ICacheService CacheService;
    private static readonly IAbstractionLicenceCacheService AbsLicCacheService;
    
    private static readonly FileSystemCacheService? RealCacheService;
    private static readonly FileSystemAbstractionLicenceCacheService? RealAbsLicCacheService;
    
    static PdfPigNoOcrPdfTests3()
    {
        RealCacheService = new FileSystemCacheService("Cache/");
        RealAbsLicCacheService = new FileSystemAbstractionLicenceCacheService("Cache/");

        (CacheService, AbsLicCacheService) = GeneralTestsHelper.GetFakeCacheService(
            RealCacheService,
            RealAbsLicCacheService,
            NaldData,
            [],
            FileLicenceMappingWithout52);
        
        NaldDataLookupService = new NaldDataLookupService(AbsLicCacheService);
    }
    
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider =
        new(TestConfig.PostgresHost,
            TestConfig.PostgresPort,
            TestConfig.PostgresDbName,
            TestConfig.PostgresUsername,
            TestConfig.PostgresPassword,
            maxPoolSize: 10);

    private static IAbstractionLicenceDatabaseReadService ReadService =>
        new PostgresAbstractionLicenceReadService(NpgsqlDataSourceProvider);

    private static readonly IAbstractionLicenceCacheService DatabaseCacheService =
        new DatabaseAbstractionLicenceCacheService(ReadService, null!);
    
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
            // TODO mock of an OCR service that errors if called
        },
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService,
        MessageQueueService);
    
    private static readonly int NoneNeRegionCode = 1;
    private static readonly int NeRegionCode = 3;
    
    private static Dictionary<string, DmsFileData> FileLicenceMappingWithout52 =>
        new()
        {
            { 
                FormattingHelper.StripForComparison("25 68 001 247", NeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10892721.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10892721.pdf"),
                    DmsPath = "Something to look for"
                }
            },
            {
                FormattingHelper.StripForComparison("25 68 001 248", NeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893422.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893422.pdf"),
                    DmsPath = "Something to look for"
                }
            },
            {
                FormattingHelper.StripForComparison("NE/026/0034/018", NeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf"),
                    DmsPath = "Something to look for"
                }
            }
        };
    
    private static readonly Dictionary<string, List<NaldAbstractionData>> NaldData = GetNaldData();

    private static Dictionary<string, List<NaldAbstractionData>> GetNaldData()
    {
        var returnList = new Dictionary<string, List<NaldAbstractionData>>
        {
            {
                "1|2568001247",
                [
                    new NaldAbstractionData
                    {
                        AsrcCode = "G",
                        LicenceNumber = "25/68/001/247",
                        FgacRegionCode = 1
                    }
                ]
            },
            {
                "1|2568001248",
                [
                    new NaldAbstractionData
                    {
                        AsrcCode = "S",
                        LicenceNumber = "25/68/001/248",
                        FgacRegionCode = 1
                    }
                ]
            },
            {
                "1|2568001249",
                [
                    new NaldAbstractionData
                    {
                        AsrcCode = "S",
                        LicenceNumber = "25/68/001/249",
                        FgacRegionCode = 1
                    }
                ]
            }
        };

        return returnList;
    }

    private async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode, int _, string pdfFolder)
    {
        return new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            await fixture.FirstNamesCsvTask(),
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            await fixture.GetLicenceNumbersServiceAsync((short)regionCode, DatabaseCacheService),
            new DmsLookupService(),
            regionCode,
            DateTime.Now,
            useLockExclusivity: false);
    }
    
    private async Task<MatchesResult> GetMatchesAsync(
        string fileName,
        int regionCode,
        int _ = 1,
        int fileLicenceMapping = 1,
        ICacheService? cacheService = null)
    {
        var lookupConfig = await LookupConfigurationAsync(regionCode, fileLicenceMapping, TestConfig.PdfFolder);
        
        if (cacheService != null)
        {
            lookupConfig.CacheService = cacheService;
        }
        
        return (await _pdfDataExtractor.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            lookupConfig,
            [fileName],
            0)).Item!;
    }
    
    [Fact]
    public async Task WhenNearPreviousLineIsCompany_SimpleAbstractionLimits1LicenceToLicenceLink_ThenFoundCorrectly()
    {
        // Arrange
        var specialCacheServices =
            GeneralTestsHelper.GetFakeCacheService(
                RealCacheService!,
                RealAbsLicCacheService!,
                NaldData,
                [],
                FileLicenceMappingWithout52);

        const string filename = "Application Minor Variation Issued Licence 11.12.2019 11149448.pdf";

        // Act
        var resultFull = await GetMatchesAsync(
            filename,
            3,
            fileLicenceMapping: 2,
            cacheService: specialCacheServices.CacheService);

        var resultList = resultFull.Matches!;

        // Assert

        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);

        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(10, records.Text!.Count);

        var additionalInformation =
            resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(32, additionalInformation.Text!.Count);

        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");

        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Rolawn Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);

        var abstractionLimitsSection =
            resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");

        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.NotNull(abstractionLimitsSection.Text);
        Assert.Equal(12, abstractionLimitsSection.Text.Count);
        Assert.Equal("200,000 cubic metres per year.",
            abstractionLimitsSection.Text![11].Text);
        Assert.Equal(2, abstractionLimitsSection.SubResults.Count);
        Assert.Equal(7, abstractionLimitsSection.SubResults[0].Text!.Count);

        var point1 = abstractionLimitsSection.SubResults[0];
        var point1Sub1 = point1.SubResults[0];

        Assert.Equal("120", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                                 && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!
            .First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                                 && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!
            .First().Text);
        Assert.Equal("2600", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                                 && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!
            .First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                                 && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!
            .First().Text);
        Assert.Equal("60000", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                                 && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!
            .First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                                 && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!
            .First().Text);
        Assert.Equal("33.3", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                                 && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)
            ?.Text!.First().Text);
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                                 && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)
            ?.Text!.First().Text);
        Assert.Equal("60000", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                                 && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!
            .First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                                 && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!
            .First().Text);

        // TODO

        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");

        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("NE/027/0028/059", licenceNumberResult.Text!.FirstOrDefault()?.Text);

        var config = await LookupConfigurationAsync(1, 2, TestConfig.PdfFolder);
        config.CacheService = specialCacheServices.CacheService;
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            config,
            AbsLicCacheService, NaldDataLookupService)).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Single(primaryLicence.LinkedLicences);

        Assert.Equal("NE/026/0034/052", primaryLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, primaryLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("FurtherConditions", primaryLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", primaryLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal("Additional", primaryLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("AggregateConditions", primaryLicence.LinkedLicences[0].ContainedIn![2].LinkReason);
    }
}