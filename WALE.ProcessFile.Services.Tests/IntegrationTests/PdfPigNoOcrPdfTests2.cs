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
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.Services.Cache.AbstractionLicence;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[EnableParallelization]
[Collection("First Names 1")]
public class PdfPigNoOcrPdfTests2(SingletonFirstNamesFixture firstNamesFixture)
{
    private static readonly ICacheService CacheService;
    private static readonly IAbstractionLicenceCacheService AbsLicCacheService;

    static PdfPigNoOcrPdfTests2()
    {
        var realCacheService = new FileSystemCacheService("Cache/");
        var realAbsLicCacheService = new FileSystemAbstractionLicenceCacheService("Cache/");

        (CacheService, AbsLicCacheService) = GeneralTestsHelper.GetFakeCacheService(
            realCacheService,
            realAbsLicCacheService,
            NaldData,
            FileLicenceMapping);
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
    
    private static Dictionary<string, DmsFileData> FileLicenceMapping =>
        new()
        {
            { 
                FormattingHelper.StripForComparison("25 68 001 247", NoneNeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10892721.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10892721.pdf"),
                    DmsPath = "Something to look for"
                }
            },
            {
                FormattingHelper.StripForComparison("25 68 001 248", NoneNeRegionCode)!,
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
            },
            {
                FormattingHelper.StripForComparison("NE/026/0034/052", NeRegionCode)!,
                new DmsFileData
                {
                    DestinationFileName = "NE0260034052__Application Apportionment Issued Licence 11.12.2019 11149440.pdf",
                    FileId = GuidHelper.GetConsistentFileIdFromFilename("NE0260034052__Application Apportionment Issued Licence 11.12.2019 11149440.pdf"),
                    DmsPath = "Something to look for"
                }
            }
        };
    
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
    
    private readonly NaldLicenceStatusData _naldLicenceStatusData = new()
    {
        LiveLicences = [
            "2568001247",
            "2568001249"
        ],
        LapsedLicences = [],
        ExpiredLicences = [],
        RevokedLicences = [],
        ImpoundmentLicences = []
    };
    
    private static readonly Dictionary<string, List<NaldData>> NaldData = GetNaldData();

    private static Dictionary<string, List<NaldData>> GetNaldData()
    {
        var returnList = new Dictionary<string, List<NaldData>>
        {
            {
                "1|2568001247",
                [
                    new NaldData
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
                    new NaldData
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
                    new NaldData
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

    private async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode, int fileLicenceMapping, string pdfFolder)
    {
        return new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            await firstNamesFixture.FirstNamesCsvTask(),
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            await firstNamesFixture.GetLicenceNumbersServiceAsync((short)regionCode, DatabaseCacheService),
            regionCode,
            DateTime.Now,
            useLockExclusivity: false);
    }
    
    private async Task<MatchesResult> GetMatchesAsync(
        string fileName,
        int regionCode,
        int folderNumber = 1,
        int fileLicenceMapping = 1,
        ICacheService? cacheService = null)
    {
        var pdfFolder = folderNumber == 1 ? TestConfig.PdfFolder : TestConfig.PdfFolder2;
        if (folderNumber == 3) pdfFolder = TestConfig.PdfFolder3;
        if (folderNumber == 5) pdfFolder = TestConfig.PdfFolder5;

        var lookupConfig = await LookupConfigurationAsync(regionCode, fileLicenceMapping, pdfFolder);
        
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
    public async Task WhenObscureCompanyName_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {

        const string filename = "Application NA New Issued Licence 11765926.pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(13, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(11, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(44, additionalInformation.Text!.Count);
        
        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);          
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");     
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Chillingham Water Users", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(8, abstractionLimitsSection.Text!.Count);

        Assert.Single(abstractionLimitsSection.SubResults);

        var abstractionLimitsPoint = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint.SubResults);
        
        var point1Sub1 = abstractionLimitsPoint.SubResults[0];
        Assert.Equal(10, point1Sub1.SubResults.Count);

        Assert.Equal("2", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("30", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("11000", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("0.6", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)?.Text!.First().Text);                
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)?.Text!.First().Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("NE/021/0000/036", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(primaryLicence.LinkedLicences);
    }

    [Fact]
    public async Task WhenPersonalNameNoTitle_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {

        const string filename = "Application - New - Issued Licence 31.01.2017 9655530.pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);        

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(10, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(27, additionalInformation.Text!.Count);
        
        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);  
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Christopher Marler", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(10, abstractionLimitsSection.Text!.Count);
        Assert.Single(abstractionLimitsSection.SubResults);

        var sectionPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(sectionPoint1.SubResults);

        var point1Sub1 = sectionPoint1.SubResults[0];
        Assert.Equal(10, point1Sub1.SubResults.Count);

        Assert.Equal("43.2", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("1037", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);        
        Assert.Equal("37000", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);        
        Assert.Equal("12", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)?.Text!.First().Text);                
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)?.Text!.First().Text);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("4/29/04/*S/0098/R01", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(primaryLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When1_ThenFoundCorrectly()
    {
        const string filename = "ne0270018009__Application – Formal Variation – Issued Licence 19122022.pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 2);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);        

        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);  
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Equal("NE/027/0018/009", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(18, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.Equal("2/27/09/025", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);

        Assert.Equal("NE/027/0018/033", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        Assert.Equal("2/27/18/053", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);

        Assert.NotNull(agreedSchemaLicence.Points);
        Assert.Single(agreedSchemaLicence.Points);
        Assert.Equal("A", agreedSchemaLicence.Points[0].Name);
        Assert.Single(agreedSchemaLicence.Points[0].NationalGridReferences!);
        Assert.Equal("SE 57396 22415", agreedSchemaLicence.Points[0].NationalGridReferences![0].ToString());
        Assert.Equal("2.1", agreedSchemaLicence.Points[0].Id);
        Assert.Equal("A", agreedSchemaLicence.Points[0].AltId);
        Assert.Equal("At National Grid Reference SE 57396 22415 marked 'A' on the map", agreedSchemaLicence.Points[0].Description1);
        
        Assert.NotNull(agreedSchemaLicence.Purposes);
        Assert.Equal(2, agreedSchemaLicence.Purposes.Length);
        Assert.Equal("4.1", agreedSchemaLicence.Purposes[0].Id);
        Assert.Equal("Spray irrigation", agreedSchemaLicence.Purposes[0].Description);
        Assert.Equal("4.2", agreedSchemaLicence.Purposes[1].Id);
        Assert.Equal("Trickle irrigation", agreedSchemaLicence.Purposes[1].Description);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual.Length);
        
        Assert.Equal("6.1", agreedSchemaLicence.AbstractionLimits.Individual[0].DocumentIdentifier);
        Assert.Equal("2.1", agreedSchemaLicence.AbstractionLimits.Individual[0].Points![0].Id);
        Assert.True(agreedSchemaLicence.AbstractionLimits.Individual[0].Points![0].IsImplicit);
        Assert.Equal("4.1", agreedSchemaLicence.AbstractionLimits.Individual[0].Purposes![0].Id);
        Assert.False(agreedSchemaLicence.AbstractionLimits.Individual[0].Purposes![0].IsImplicit);
        
        Assert.Equal("6.2", agreedSchemaLicence.AbstractionLimits.Individual[1].DocumentIdentifier);
        Assert.Equal("2.1", agreedSchemaLicence.AbstractionLimits.Individual[1].Points![0].Id);
        Assert.True(agreedSchemaLicence.AbstractionLimits.Individual[1].Points![0].IsImplicit);
        Assert.Equal("4.2", agreedSchemaLicence.AbstractionLimits.Individual[1].Purposes![0].Id);
        Assert.False(agreedSchemaLicence.AbstractionLimits.Individual[1].Purposes![0].IsImplicit);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates!.Length);
        
        Assert.Equal("6.3", agreedSchemaLicence.AbstractionLimits.Aggregates[0].DocumentIdentifier);
        Assert.Equal("2.1", agreedSchemaLicence.AbstractionLimits.Aggregates[0].Points![0].Id);
        Assert.True(agreedSchemaLicence.AbstractionLimits.Aggregates[0].Points![0].IsImplicit);
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Aggregates[0].Purposes);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates[0].Purposes!.Length);
        Assert.Equal("4.1", agreedSchemaLicence.AbstractionLimits.Aggregates[0].Purposes![0].Id);
        Assert.False(agreedSchemaLicence.AbstractionLimits.Aggregates[0].Purposes![0].IsImplicit);
        Assert.Equal("4.2", agreedSchemaLicence.AbstractionLimits.Aggregates[0].Purposes![1].Id);
        Assert.False(agreedSchemaLicence.AbstractionLimits.Aggregates[0].Purposes![0].IsImplicit);        
        
        Assert.Equal("6.4", agreedSchemaLicence.AbstractionLimits.Aggregates[1].DocumentIdentifier);
        Assert.Equal("2.1", agreedSchemaLicence.AbstractionLimits.Aggregates[1].Points![0].Id);
        Assert.True(agreedSchemaLicence.AbstractionLimits.Aggregates[1].Points![0].IsImplicit);
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Aggregates[1].Purposes);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates[1].Purposes!.Length);
        Assert.Equal("4.2", agreedSchemaLicence.AbstractionLimits.Aggregates[1].Purposes![0].Id);
        Assert.False(agreedSchemaLicence.AbstractionLimits.Aggregates[1].Purposes![0].IsImplicit);
        Assert.Equal("4.1", agreedSchemaLicence.AbstractionLimits.Aggregates[1].Purposes![1].Id);
        Assert.False(agreedSchemaLicence.AbstractionLimits.Aggregates[1].Purposes![0].IsImplicit);
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Aggregates[1].LinkedLicences);
        Assert.Equal(17, agreedSchemaLicence.AbstractionLimits.Aggregates[1].LinkedLicences!.Length);
    }
    
    [Fact]
    public async Task WhenMultipleNamesWithNoTitle_And3ConditionsOfAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {

        const string filename = "Application Issued New Licence 2 23.2.2024.pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);        

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(13, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(64, additionalInformation.Text!.Count);
        
        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);  
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Clemency Ives, Stephanie Williams, Octavia Williams, trading as Brickworth Park Farms",
            string.Join(", ", nameResult.Text!.Select(x => x.Text)));
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel!.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(26, abstractionLimitsSection.Text!.Count);

        Assert.Equal(3, abstractionLimitsSection.SubResults.Count);

        var point1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(point1.SubResults);

        var point1Sub1 = point1.SubResults[0];
        Assert.Equal(10, point1Sub1.SubResults.Count);

        var pointName = point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition")?.Text!.First().Text;
        
        Assert.Equal("2.1", pointName);
        
        Assert.Equal("90", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
            && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("2160", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);   
        Assert.Equal("113650", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("25.3", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);           

        // TODO add a test for the futher conditions 90,923
        
        Assert.Equal("25.3", point1Sub1.SubResults[9].Text!.First().Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("SO/042/0036/022", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Equal("SO/042/0036/022", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.Equal("SO/042/0036/023", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);

        Assert.Equal("36/134", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("LapsedLicence", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        Assert.Equal("SO/042/0036/024", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task WhenCompanyNameBeforeLabelWhenUsuallyAfter_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application New Licence July 2017 9867755.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(9, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(22, additionalInformation.Text!.Count);
        
        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);  
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");     
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Canterbury Golf Club Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(10, abstractionLimitsSection.Text!.Count);
        Assert.Single(abstractionLimitsSection.SubResults);

        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint1.SubResults);

        var point1Sub1 = abstractionLimitsPoint1.SubResults[0];
        Assert.Equal(10, point1Sub1.SubResults.Count);
        
        Assert.Equal("3.5", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("30", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("8300", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("0.97", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("SO/040/0009/016", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenX_EveyrhtingFoundButListSayingOtherwise_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application NA Formal Variation Licence 08122021.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(10, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(30, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");     
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("D.& M.Gedney Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(26, abstractionLimitsSection.Text!.Count);
        Assert.Equal(4, abstractionLimitsSection.SubResults.Count);

        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint1.SubResults);

        var point1Sub1 = abstractionLimitsPoint1.SubResults[0];
        Assert.Equal(8, point1Sub1.SubResults.Count);
        
        Assert.Equal("14", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("112", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("22731", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        
        // TODO, 3 other points
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("9/40/01/0500/G", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task Z_Z_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application - formal variation - issue licence 9227047.pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(10, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(17, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal("4. PURPOSE OF ABSTRACTION", purposeResult.Text?[0].Text);
        Assert.Equal("4.1 Public water supply.", purposeResult.Text?[1].Text);
        Assert.Equal(["PURPOSE OF ABSTRACTION"], purposeResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.BetweenLabels, purposeResult.MatchedPosition);

        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");     
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Thames Water Utilities Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(18, abstractionLimitsSection.Text!.Count);
        Assert.Equal(3, abstractionLimitsSection.SubResults.Count);

        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint1.SubResults);

        var point1Sub1 = abstractionLimitsPoint1.SubResults[0];
        Assert.Equal(10, point1Sub1.SubResults.Count);

        Assert.Equal("Up to and including 31 March 2025", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Date"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("Up to and including ") == true)?.Text![0].Text);
        
        Assert.Equal("215", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("4550", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("1460000", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("59.7", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);
        
        var abstractionLimitsPoint2 = abstractionLimitsSection.SubResults[1];
        Assert.Single(abstractionLimitsPoint2.SubResults);
        
        var point2Sub1 = abstractionLimitsPoint2.SubResults[0];
        Assert.Equal(10, point2Sub1.SubResults.Count);

        Assert.Equal("From 01 April 2025", point2Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Date"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("From ") == true)?.Text![0].Text);
        
        var abstractionLimitsPoint3 = abstractionLimitsSection.SubResults[2];
        Assert.Single(abstractionLimitsPoint3.SubResults);
        
        var point3Sub1 = abstractionLimitsPoint3.SubResults[0];
        Assert.Equal(8, point3Sub1.SubResults.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("08/37/54/0025", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Single(agreedSchemaLicence.LinkedLicences);

        Assert.Equal("8/37/54/0061/R01", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }    
    
    [Fact]
    public async Task WhenABC_DEF_ThenY()
    {
        // Arrange

        const string filename = "06_transfer_application_new_licence_issued_2112018_10555534.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(25, additionalInformation.Text!.Count);
        
        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);  
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Brett Aggregates Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("TH/039/0028/051", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var meansOfAbstraction = resultList.FirstOrDefault(
            result => result.LabelGroupName == "MeansOfAbstraction");
        
        Assert.NotNull(meansOfAbstraction);
        Assert.False(meansOfAbstraction.IsOcr);
        Assert.Single(meansOfAbstraction.Text!);
        
        Assert.Single(meansOfAbstraction.SubResults);
        Assert.Equal(4, meansOfAbstraction.SubResults[0].SubResults.Count);
        
        var textStr = meansOfAbstraction.SubResults[0].SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "TextWithoutNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("A pump with capacity not exceeding 86 litres per second", textStr);
        
        var perSecond = meansOfAbstraction.SubResults[0].SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("86", perSecond);
        
        var perSecondUnits = meansOfAbstraction.SubResults[0].SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);   
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal("4. PURPOSE OF ABSTRACTION", purposeResult.Text?[0].Text);
        Assert.Equal("4.1 Transfer for the purpose of dewatering.", purposeResult.Text?[1].Text);
        Assert.Equal(["PURPOSE OF ABSTRACTION"], purposeResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.BetweenLabels, purposeResult.MatchedPosition);
        
        Assert.Single(purposeResult.SubResults);
        var firstPurposePointGroup = purposeResult.SubResults.First();
        Assert.Equal("4.1 Transfer for the purpose of dewatering.", firstPurposePointGroup.Text!.First().Text);
        
        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();

        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("TH/039/0028/051", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal("LV2018110220260331", agreedSchemaLicence.LicenceVersion.LicenceVersionId);
        Assert.Equal(new DateTime(2018, 11, 02), agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Equal(new DateTime(2026, 03, 31), agreedSchemaLicence.LicenceVersion.ExpiryDate);
        Assert.Equal(new DateTime(2018, 11, 02), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Equal(filename, agreedSchemaLicence.Filename);

        Assert.Single(agreedSchemaLicence.PeriodsOfAbstraction);
        Assert.Single(agreedSchemaLicence.Points);
        Assert.Single(agreedSchemaLicence.MeansOfAbstraction);
        Assert.Single(agreedSchemaLicence.Purposes);
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Individual);

        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("DewateringDischargeCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task WhenABCD_DEF_ThenY()
    {
        // Arrange

        const string filename = "1.3-licence-07.02.2023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);

        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("South West Water Limited", companyName?.Text?.FirstOrDefault()?.Text);

        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService);
        
        Assert.Equal(3, licenceSets.Count);
        var agreedSchemaLicenceGroup = licenceSets[1];
        
        Assert.Equal(3, agreedSchemaLicenceGroup.Licences.Length);

        Assert.Equal("SW/047/0051/003", agreedSchemaLicenceGroup.Licences[0].LicenceNumber?.Value);
        Assert.Equal("15/47/013/S/020", agreedSchemaLicenceGroup.Licences[1].LicenceNumber?.Value);
        Assert.Equal("15/47/52/I/1", agreedSchemaLicenceGroup.Licences[2].LicenceNumber?.Value);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal("SW0470051003-LV2023020720380331", agreedSchemaLicence.Id);
        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("SW/047/0051/003", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal("LV2023020720380331", agreedSchemaLicence.LicenceVersion.LicenceVersionId);
        Assert.Equal(new DateTime(2023, 02, 07), agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Equal(new DateTime(2038, 03, 31), agreedSchemaLicence.LicenceVersion.ExpiryDate);
        Assert.Equal(new DateTime(2023, 02, 07), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Equal(filename, agreedSchemaLicence.Filename);

        Assert.Single(agreedSchemaLicence.Points);
        Assert.Single(agreedSchemaLicence.MeansOfAbstraction);
        Assert.Single(agreedSchemaLicence.Purposes);
        
        Assert.Single(agreedSchemaLicence.PeriodsOfAbstraction);
        Assert.Equal("From 1 November to 31 March inclusive", agreedSchemaLicence.PeriodsOfAbstraction.Single().Description);
        Assert.Equal("1 November", agreedSchemaLicence.PeriodsOfAbstraction.Single().StartDate);
        Assert.Equal("31 March", agreedSchemaLicence.PeriodsOfAbstraction.Single().EndDate);
        Assert.Equal("5.1", agreedSchemaLicence.PeriodsOfAbstraction.Single().Id);
        Assert.Equal(true, agreedSchemaLicence.PeriodsOfAbstraction.Single().Inclusive);
        
        Assert.Equal(4, agreedSchemaLicence.AbstractionLimits.Individual![0].Limits.Count);
        var limitGroup = agreedSchemaLicence.AbstractionLimits.Individual[0];
        
        Assert.Equal(2000, limitGroup.Limits[0].Value);
        Assert.Equal("cubic metres", limitGroup.Limits[0].Units);        
        Assert.Equal(LimitPeriodType.PerHour, limitGroup.Limits[0].PeriodType);
        Assert.Equal(40000, limitGroup.Limits[1].Value);
        Assert.Equal(6000000, limitGroup.Limits[2].Value);
        Assert.Equal(556, limitGroup.Limits[3].Value);        

        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates!);
        Assert.Equal("SW0470051003-LV2023020720380331-LL-1547013S020",
            agreedSchemaLicence.AbstractionLimits.Aggregates![0].Id);
        Assert.Equal("LV2023020720380331",
            agreedSchemaLicence.AbstractionLimits.Aggregates[0].SourceLicenceVersionId);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal(148000, agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerDay, agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits[0].PeriodType);
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits[0].Units);
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Equal("1 April", agreedSchemaLicence.DefinitionOfYear.StartDate);
        Assert.Equal("31 March", agreedSchemaLicence.DefinitionOfYear.EndDate);        
        
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length);

        Assert.Equal("15/47/013/S/020", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task When_AbstractionLicence7310604_ThenY()
    {
        // Arrange

        const string filename = "Abstraction Licence 7310604.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        
        var abstractionLimitsResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.False(abstractionLimitsResult.IsOcr);
        Assert.Equal(16, abstractionLimitsResult.Text!.Count);
        Assert.Equal(15, abstractionLimitsResult.LabelStartLineNumber);
        
        Assert.NotNull(abstractionLimitsResult.SubResults);        
        Assert.Equal(3, abstractionLimitsResult.SubResults.Count);
        Assert.Equal(15, abstractionLimitsResult.LabelStartLineNumber);
        
        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal("6.1", abstractionLimitsSection1.MatchedLabelTextFirstLine);
        Assert.Equal(4, abstractionLimitsSection1.Text!.Count);
        Assert.NotNull(abstractionLimitsSection1.SubResults);
        Assert.Single(abstractionLimitsSection1.SubResults);
        var section1Sub1 = abstractionLimitsSection1.SubResults[0];
        Assert.Equal(9, section1Sub1.SubResults.Count);
        
        var abstractionLimitsSection2 = abstractionLimitsResult.SubResults[1];
        Assert.Equal("6.2", abstractionLimitsSection2.MatchedLabelTextFirstLine);
        Assert.Equal(4, abstractionLimitsSection2.Text!.Count);
        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults);
        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(9, section2Sub1.SubResults.Count);
        
        var abstractionLimitsSection3 = abstractionLimitsResult.SubResults[2];
        Assert.Equal("6.3", abstractionLimitsSection3.MatchedLabelTextFirstLine);
        Assert.Equal(6, abstractionLimitsSection3.Text!.Count); // TODO should really be 5, its including a header from the next page
        Assert.NotNull(abstractionLimitsSection3.SubResults);
        Assert.Single(abstractionLimitsSection3.SubResults);
        var section3Sub1 = abstractionLimitsSection3.SubResults[0];
        Assert.Equal(6, section3Sub1.SubResults.Count);

        Assert.Equal("6.3", section3Sub1.SubResults[0].Text!.FirstOrDefault()!.Text);
        Assert.Equal("cubic metres", section3Sub1.SubResults[1].Text!.FirstOrDefault()!.Text);
        Assert.Equal("cubic metres", section3Sub1.SubResults[2].Text!.FirstOrDefault()!.Text);
        Assert.Equal("15", section3Sub1.SubResults[3].Text!.FirstOrDefault()!.Text);
        Assert.Equal("360", section3Sub1.SubResults[4].Text!.FirstOrDefault()!.Text);
        Assert.Equal("1 January and ending on 31 December", section3Sub1.SubResults[5].Text!.FirstOrDefault()!.Text);
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.Equal(2, points!.Text!.Count);
        Assert.Equal("2.1. At National Grid Reference TA 04990 38509 at the point marked \"A\" on the", points.Text![0].Text);
        Assert.Equal("map.", points.Text![1].Text);
        
        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("Lakeminster Park Limited", companyName?.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();
        
        Assert.Single(agreedSchemaLicenceGroup.Licences);
        Assert.Equal("2/26/32/328", agreedSchemaLicenceGroup.Licences[0].LicenceNumber?.Value);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Equal("2/26/32/328", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
        
        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("2/26/32/328", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(new DateTime(2012, 08, 16), agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Equal(new DateTime(1993, 06, 23), agreedSchemaLicence.LicenceVersion.OriginalIssueDate);
        Assert.Equal(new DateTime(2012, 08, 16), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Equal("22632328-LV20120816", agreedSchemaLicence.Id);
        Assert.Equal("LV20120816", agreedSchemaLicence.LicenceVersion.LicenceVersionId);

        Assert.Single(agreedSchemaLicence.Points);
        Assert.Single(agreedSchemaLicence.MeansOfAbstraction);
        Assert.Equal(2, agreedSchemaLicence.Purposes.Length);
        
        Assert.Single(agreedSchemaLicence.PeriodsOfAbstraction);
        Assert.Equal("All Year", agreedSchemaLicence.PeriodsOfAbstraction.Single().Description);
        Assert.NotNull(agreedSchemaLicence.PeriodsOfAbstraction.Single().StartDate);
        Assert.Null(agreedSchemaLicence.PeriodsOfAbstraction.Single().EndDate);
        Assert.Equal("5.1", agreedSchemaLicence.PeriodsOfAbstraction.Single().Id);
        Assert.False(agreedSchemaLicence.PeriodsOfAbstraction.Single().Inclusive);
        
        Assert.Equal(10, agreedSchemaLicence.AbstractionLimits.Individual![0].Limits.Count);

        var limitGroup = agreedSchemaLicence.AbstractionLimits.Individual[0];
        
        Assert.Equal("cubic metres", limitGroup.Limits[0].Units);        
        Assert.Equal(LimitPeriodType.PerHour, limitGroup.Limits[0].PeriodType);
        Assert.Equal(15, limitGroup.Limits[0].Value);
        Assert.Equal(360, limitGroup.Limits[1].Value);
        Assert.Equal(43180, limitGroup.Limits[2].Value);
        Assert.Equal(0.42, limitGroup.Limits[3].Value);
        Assert.Equal(15, limitGroup.Limits[4].Value);
        Assert.Equal(360, limitGroup.Limits[5].Value);
        Assert.Equal(2270, limitGroup.Limits[6].Value);
        Assert.Equal(0.42, limitGroup.Limits[7].Value);

        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Equal("1 January", agreedSchemaLicence.DefinitionOfYear.StartDate);
        Assert.Equal("31 December", agreedSchemaLicence.DefinitionOfYear.EndDate);        
    }
    
    [Fact]
    public async Task When_YorkshireWaterCompany3_ThenY()
    {
        // Arrange

        const string filename = "Application - New - Licence Issued 30092021.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);

        var purposeGroup = points.SubResults.Single();
        
        var actualPoints = purposeGroup.SubResults;
        Assert.Equal(5, actualPoints.Count);
        
        Assert.Equal(10, points.Text!.Count);
        Assert.StartsWith("2.1 Winscar Reservoir at National Grid Re", points.Text![0].Text);
        
        var purposes = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Purposes");
        Assert.NotNull(purposes);

        var purposesSub = purposes.SubResults;
        Assert.Single(purposesSub);
        
        Assert.Equal(2, purposesSub[0].SubResults
            .Where(sr => sr.MatchedLabel?.Name == "Purposes")
            .ToList()
            .Count);
        
        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("Yorkshire", companyName?.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();
        
        Assert.Single(agreedSchemaLicenceGroup.Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("2/27/05/026", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(new DateTime(2021, 09, 30), agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Equal(new DateTime(1965, 12, 07), agreedSchemaLicence.LicenceVersion.OriginalIssueDate);
        Assert.Equal(new DateTime(2021, 09, 30), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Equal("22705026-LV20210930", agreedSchemaLicence.Id);
        Assert.Equal("LV20210930", agreedSchemaLicence.LicenceVersion.LicenceVersionId);

        Assert.NotNull(agreedSchemaLicence.Points);
        Assert.Equal(5, agreedSchemaLicence.Points.Length);
        
        var point = agreedSchemaLicence.Points[0];
        Assert.Equal("2.1", point.Id);
        Assert.EndsWith("National Grid Reference SE 15454 02535 marked \"A\" on the map", point.Description1);
        
        point = agreedSchemaLicence.Points[1];
        Assert.Equal("2.2", point.Id);
        Assert.EndsWith("National Grid Reference SE 15253 01352 marked \"B\" on the map", point.Description1);
        
        point = agreedSchemaLicence.Points[2];
        Assert.Equal("2.3", point.Id);
        Assert.EndsWith("National Grid Reference SE 15820 01918 marked \"C\" on the map", point.Description1);
        
        point = agreedSchemaLicence.Points[3];
        Assert.Equal("2.4", point.Id);
        Assert.EndsWith("National Grid Reference SE 15192 03582 marked \"D\" on the map", point.Description1);
        
        point = agreedSchemaLicence.Points[4];
        Assert.Equal("2.5", point.Id);
        Assert.EndsWith("National Grid Reference SE 13596 03969 marked \"E\" on the map", point.Description1); // TODO should be "E" not "E
        
        Assert.NotNull(agreedSchemaLicence.Purposes);
        Assert.Equal(2, agreedSchemaLicence.Purposes.Length);
        
        var purpose = agreedSchemaLicence.Purposes[0];
        Assert.Equal("4.1", purpose.Id);
        Assert.Equal("Public water supply", purpose.Description);
        
        purpose = agreedSchemaLicence.Purposes[1];
        Assert.Equal("4.2", purpose.Id);
        Assert.StartsWith("Transfer from W", purpose.Description);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits);
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual.Length);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits.Count);

        var limitG = agreedSchemaLicence.AbstractionLimits.Individual[0];
        var limit = limitG.Limits[0];
        
        Assert.Single(limitG.Purposes!);
        Assert.Equal("4.1", limitG.Purposes![0].Id);
        Assert.Equal(38640, limit.Value);

        limit = limitG.Limits[1];
        Assert.Single(limitG.Purposes!);
        Assert.Equal("4.1", limitG.Purposes![0].Id);
        Assert.Equal(10140000, limit.Value);

        limitG = agreedSchemaLicence.AbstractionLimits.Individual[1];
        limit = limitG.Limits[0];
        
        Assert.Single(limitG.Purposes!);
        Assert.Equal("4.2", limitG.Purposes![0].Id);
        Assert.Equal(2482000, limit.Value);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates);

        var aggregate = agreedSchemaLicence.AbstractionLimits.Aggregates[0];
        Assert.Equal("22705026-LV20210930-ILPU", aggregate.Id);
        Assert.Equal(2, aggregate.Purposes!.Length);
        Assert.Equal("4.1", aggregate.Purposes[0].Id);
        Assert.Equal("4.2", aggregate.Purposes[1].Id);
        
        Assert.Equal(2, aggregate.Limits.Count);

        Assert.Equal(38640, aggregate.Limits[0].Value);
        Assert.Null(aggregate.Limits[0].Purposes);
        Assert.Null(aggregate.Limits[0].Points);
        Assert.Equal(5, aggregate.Points.Length);
        Assert.Equal(0, aggregate.Points.Count(c => c.IsImplicit != true));
        Assert.Equal(10140000, aggregate.Limits[1].Value);
        Assert.Null(aggregate.Limits[1].Purposes);
        Assert.Null(aggregate.Limits[1].Points);
        Assert.Equal(5, aggregate.Points.Length);
        Assert.Equal(0, aggregate.Points.Count(c => c.IsImplicit != true));
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_YorkshireWaterCompany4_ThenY()
    {
        // Arrange

        const string filename = "Application Formal Variation Issued Licence 07032023 (1).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        Assert.Equal(9, points.Text!.Count);
        Assert.StartsWith("2.1 At National Grid Reference SE 069 076", points.Text![0].Text);

        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("Yorkshire", companyName?.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();
        
        Assert.Single(agreedSchemaLicenceGroup.Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("2/27/11/065", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(new DateTime(2023, 03, 07), agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Equal(new DateTime(1966, 01, 27), agreedSchemaLicence.LicenceVersion.OriginalIssueDate);
        Assert.Equal(new DateTime(2023, 03, 07), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Equal("22711065-LV20230307", agreedSchemaLicence.Id);
        Assert.Equal("LV20230307", agreedSchemaLicence.LicenceVersion.LicenceVersionId);

        Assert.Equal(5, agreedSchemaLicence.Points.Length);
        Assert.Equal(2, agreedSchemaLicence.Purposes.Length);
        
        var point = agreedSchemaLicence.Points[0];
        Assert.Equal("2.1", point.Id);
        Assert.EndsWith("At National Grid Reference SE 069 076 marked 'A' on map 2", point.Description1);
        
        point = agreedSchemaLicence.Points[1];
        Assert.Equal("2.2", point.Id);
        Assert.EndsWith("At National Grid Reference SE 054 096 marked 'B' on map 2", point.Description1);
        
        point = agreedSchemaLicence.Points[2];
        Assert.Equal("2.3", point.Id);
        Assert.EndsWith("At National Grid Reference SE 047 105 marked 'C' on map 2", point.Description1);
        
        point = agreedSchemaLicence.Points[3];
        Assert.Equal("2.4", point.Id);
        Assert.EndsWith("At National Grid Reference SE 073 115 marked 'D' on map 1", point.Description1);
        
        point = agreedSchemaLicence.Points[4];
        Assert.Equal("2.5", point.Id);
        Assert.EndsWith("At National Grid Reference SE 098 130 marked 'E' on map 1", point.Description1);
        
        Assert.NotNull(agreedSchemaLicence.Purposes);
        Assert.Equal(2, agreedSchemaLicence.Purposes.Length);
        
        var purpose = agreedSchemaLicence.Purposes[0];
        Assert.Equal("4.1", purpose.Id);
        Assert.Equal("Public water supply", purpose.Description);
        
        purpose = agreedSchemaLicence.Purposes[1];
        Assert.Equal("4.2", purpose.Id);
        Assert.StartsWith("Transfer for the purpose ", purpose.Description);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits);
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual);

        var limitG = agreedSchemaLicence.AbstractionLimits.Individual[0];
        var limit = limitG.Limits[0];

        Assert.Null(limit.Purposes);
        Assert.Equal(2, limitG.Purposes.Length);
        Assert.Equal(0, limitG.Purposes.Count(c => c.IsImplicit != true));
        Assert.Equal(12410000, limit.Value);
        
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_YorkshireWaterCompany5_ThenY()
    {
        // Arrange

        const string filename = "Application Formal Variation Issued Licence 07032023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        Assert.Equal(22, points.Text!.Count);
        Assert.StartsWith("2.1 At the following National Grid Refe", points.Text![0].Text);

        var pointsAll = points.SubResults
            .SelectMany(point => point.SubResults.Where(x => x.MatchedLabel?.Name == "Point"))
            .ToList();
        
        Assert.Equal(20, pointsAll.Count);
        Assert.StartsWith("A SE 06", pointsAll[0].Text?.FirstOrDefault()?.Text);
        Assert.StartsWith("T SE 02", pointsAll.Last().Text?.FirstOrDefault()?.Text);
        
        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("Yorkshire", companyName?.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();
        
        Assert.Single(agreedSchemaLicenceGroup.Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("2/27/11/064", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(new DateTime(2023, 03, 07), agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Equal(new DateTime(1966, 01, 27), agreedSchemaLicence.LicenceVersion.OriginalIssueDate);
        Assert.Equal(new DateTime(2023, 03, 07), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Equal("22711064-LV20230307", agreedSchemaLicence.Id);
        Assert.Equal("LV20230307", agreedSchemaLicence.LicenceVersion.LicenceVersionId);

        Assert.Equal(20, agreedSchemaLicence.Points.Length);
        
        var point = agreedSchemaLicence.Points[0];
        Assert.Equal("A", point.Id);
        Assert.StartsWith("SE 066 152", point.Description1);
        Assert.Equal(16, point.Description1!.Length);
        
        Assert.NotNull(agreedSchemaLicence.Purposes);
        Assert.Equal(2, agreedSchemaLicence.Purposes.Length);
        
        var purpose = agreedSchemaLicence.Purposes[0];
        Assert.Equal("4.1", purpose.Id);
        Assert.Equal("Public water supply", purpose.Description);
        
        purpose = agreedSchemaLicence.Purposes[1];
        Assert.Equal("4.2", purpose.Id);
        Assert.StartsWith("Transfer for the", purpose.Description);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits);
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual);

        var limitG = agreedSchemaLicence.AbstractionLimits.Individual[0];
        var limit = limitG.Limits[0];

        Assert.Null(limit.Purposes!);
        Assert.Equal(2, limitG.Purposes!.Length);
        Assert.Equal(0, limitG.Purposes.Count(c => c.IsImplicit != true));
        Assert.Null(limit.Points);
        Assert.Equal(20, limitG.Points!.Length);
        Assert.Equal(0, limitG.Points.Count(c => c.IsImplicit != true));
        Assert.Equal(5840000, limit.Value);
        
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_YorkshireWaterCompany6_ThenY()
    {
        // Arrange

        const string filename = "Application Minor Variation Issued Licence 03.10.24.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        Assert.Equal(2, points.Text!.Count);
        Assert.StartsWith("2.1 At National Grid Reference ", points.Text![0].Text);
        Assert.StartsWith("2.2 At National Grid Reference ", points.Text![1].Text);
        
        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("Yorkshire", companyName?.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();
        
        Assert.Single(agreedSchemaLicenceGroup.Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("2/27/12/261", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(new DateTime(2024, 10, 03), agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Equal(new DateTime(1966, 01, 27), agreedSchemaLicence.LicenceVersion.OriginalIssueDate);
        Assert.Equal(new DateTime(2024, 10, 03), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Equal("22712261-LV20241003", agreedSchemaLicence.Id);
        Assert.Equal("LV20241003", agreedSchemaLicence.LicenceVersion.LicenceVersionId);

        Assert.Equal(2, agreedSchemaLicence.Points.Length);
        Assert.Equal(2, agreedSchemaLicence.Purposes.Length);
        
        var point = agreedSchemaLicence.Points[0];
        Assert.Equal("2.1", point.Id);
        Assert.EndsWith("At National Grid Reference SE 039 152 marked 'A' on map 1", point.Description1);
        
        point = agreedSchemaLicence.Points[1];
        Assert.Equal("2.2", point.Id);
        Assert.EndsWith("At National Grid Reference SE 052 166 marked 'B' on map 1", point.Description1);
        
        Assert.NotNull(agreedSchemaLicence.Purposes);
        Assert.Equal(2, agreedSchemaLicence.Purposes.Length);
        
        var purpose = agreedSchemaLicence.Purposes[0];
        Assert.Equal("4.1", purpose.Id);
        Assert.Equal("Public water supply", purpose.Description);
        
        purpose = agreedSchemaLicence.Purposes[1];
        Assert.Equal("4.2", purpose.Id);
        Assert.StartsWith("Transfer for the purpose", purpose.Description);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits);
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual.Length);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[0].Limits);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[1].Limits);

        var limitG = agreedSchemaLicence.AbstractionLimits.Individual[0];
        var limit = limitG.Limits[0];

        Assert.Null(limit.Purposes);
        Assert.Equal(2, limitG.Purposes.Length);
        Assert.Equal(0, limitG.Purposes.Count(c => c.IsImplicit != true));
        Assert.Null(limit.Points!);
        Assert.Single(limitG.Points!);
        Assert.Equal(730000, limit.Value);

        limitG = agreedSchemaLicence.AbstractionLimits.Individual[1];
        limit = limitG.Limits[0];

        Assert.Null(limit.Purposes);
        Assert.Equal(2, limitG.Purposes.Length);
        Assert.Equal(0, limitG.Purposes.Count(c => c.IsImplicit != true));
        Assert.Null(limit.Points!);
        Assert.Single(limitG.Points!);
        Assert.Equal(2920000, limit.Value);
        
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_FileThatErrored_ThenY()
    {
        // Arrange

        const string filename = "Application - Minor Variation -Application New Licence Issued 28_04_2021 00_00_00 11794555.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceNumber = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("18/54/21/0116", licenceNumber.Text!.First().Text);
        
        var records = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(10, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(71, additionalInformation.Text!.Count);
        
        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Single(agreedSchemaLicence.LinkedLicences);

        Assert.Equal("18/54/21/0026", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task When_FileThatDidntGetPurposes_ThenNowGetsThem()
    {
        // Arrange

        const string filename = "22718045__Application - Reduction -Application New Licence Issued 24_06_2019 00_00_00 10897641.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 2);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var records = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(4, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(19, additionalInformation.Text!.Count);
        
        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
            
        var purposeResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Purposes");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal("4. PURPOSE OF ABSTRACTION 4.1 Cooling water make up (68% returned to source).",
            string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!));
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 2, TestConfig.PdfFolder),
            AbsLicCacheService)).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_PurposeHasAnUptoInIt_ThenNowGetsThem()
    {
        // Arrange

        const string filename = "22719149__Application Formal Variation - Issued Licence [04-09-2018] 10474343.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var records = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(17, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(35, additionalInformation.Text!.Count);
        
        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, 2, TestConfig.PdfFolder2),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.Single();
        
        Assert.Equal(2, agreedSchemaLicence.Purposes.Length);
        Assert.Equal("Power production: hydro-electric power generation", agreedSchemaLicence.Purposes[0].Description);
        Assert.Equal(CutoffType.Upto,  agreedSchemaLicence.Purposes[0].TimeCutoff!.CutoffType); 
        Assert.Equal("Up to and including 31 March 2030", agreedSchemaLicence.Purposes[0].TimeCutoff!.Date);        
        Assert.Equal("Fish farming", agreedSchemaLicence.Purposes[1].Description);
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_GTest()
    {
        // Arrange

        const string filename = "940040476g__Application – NA Formal Variation – Issued Licence 21032022.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 4, 2);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var records = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(11, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(28, additionalInformation.Text!.Count);
        
        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(4, 2, TestConfig.PdfFolder2),
            AbsLicCacheService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.Single();
        Assert.Equal("9/40/04/0476/G", agreedSchemaLicence.LicenceNumber!.Value);
        
        Assert.Equal(4, agreedSchemaLicence.Purposes.Length);
        Assert.Equal("Agriculture other than spray or trickle irrigation", agreedSchemaLicence.Purposes[0].Description);
        Assert.Equal("Spray irrigation", agreedSchemaLicence.Purposes[1].Description);
        // Ideally would check other purposes but the important thing was the licence number was read correctly 
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_PurposeHasPointsInIt_ThenNowGetsThem()
    {
        // Arrange

        const string filename = "NE0260034052__Application Apportionment Issued Licence 11.12.2019 11149440.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var records = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(14, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(36, additionalInformation.Text!.Count);

        var licenceGroups = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, 2, TestConfig.PdfFolder2),
            AbsLicCacheService);
        
        Assert.Equal(3, licenceGroups.Count);

        var agreedSchemaLicenceGroup = licenceGroups[1];
        Assert.Equal(4, agreedSchemaLicenceGroup.Licences.Length);
        
        Assert.Equal("NE/026/0034/052", agreedSchemaLicenceGroup.Licences[0].LicenceNumber?.Value);
        Assert.Equal("NE/027/0028/059", agreedSchemaLicenceGroup.Licences[1].LicenceNumber?.Value);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LicenceNumber?.Value);
        
        Assert.Equal(3, agreedSchemaLicence.Purposes.Length);
        Assert.Equal("Spray irrigation", agreedSchemaLicence.Purposes[0].Description);
        
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/027/0028/059", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("AggregateConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].LinkReason);

        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        Assert.Equal(InformationSource.Document, agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].Source);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[1].ContainedIn![1].SectionName);
        Assert.Equal("ReadInConjunction", agreedSchemaLicence.LinkedLicences[1].ContainedIn![1].LinkReason);
        Assert.Equal(InformationSource.Document, agreedSchemaLicence.LinkedLicences[1].ContainedIn![1].Source);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![2].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![2].LinkReason);
        Assert.Equal(InformationSource.OtherDocument, agreedSchemaLicence.LinkedLicences[1].ContainedIn![2].Source);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("AggregateConditions", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
        
        Assert.Equal(4, agreedSchemaLicence.Points.Length); // Table
        Assert.Equal("2.1 A", agreedSchemaLicence.Points[0].Id);
        Assert.Equal("A", agreedSchemaLicence.Points[0].AltId);
        Assert.Equal("A SE 80360 41490 Southfield Farm, Everingham, York. 1", agreedSchemaLicence.Points[0].Description1);
        Assert.Equal("2.1 B", agreedSchemaLicence.Points[1].Id);
        Assert.Equal("B", agreedSchemaLicence.Points[1].AltId);
        Assert.Equal("B SE 80490 43730 Ponds Farm, Everingham, York. 1", agreedSchemaLicence.Points[1].Description1);
        Assert.Equal("2.1 D", agreedSchemaLicence.Points[2].Id);
        Assert.Equal("D", agreedSchemaLicence.Points[2].AltId);
        Assert.Equal("D SE 70910 39340 Ellerton, East Riding of Yorkshire. 2", agreedSchemaLicence.Points[2].Description1);
        Assert.Equal("2.1 E", agreedSchemaLicence.Points[3].Id);
        Assert.Equal("E", agreedSchemaLicence.Points[3].AltId);
        Assert.Equal("E SE 73917 47832 Low Farm, Sutton Upon Derwent, Yorkshire. 3", agreedSchemaLicence.Points[3].Description1);
        
        Assert.Equal(ScrapeStatus.Ok, agreedSchemaLicence.Status);
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Individual!);
        
        Assert.Equal(8, agreedSchemaLicence.AbstractionLimits.Aggregates!.Length);
        Assert.Equal(22, agreedSchemaLicence.AbstractionLimits.Aggregates.SelectMany(x => x.Limits).Count());
        Assert.Equal(120, agreedSchemaLicence.AbstractionLimits.Aggregates![4].Limits[0].Value);
        Assert.Equal(2_600, agreedSchemaLicence.AbstractionLimits.Aggregates![4].Limits[1].Value);
        Assert.Equal(60_000, agreedSchemaLicence.AbstractionLimits.Aggregates![4].Limits[2].Value);    
    }
    
    [Fact]
    public async Task When_GettingFurtherConditions_ThenNowGetsThem()
    {
        // Arrange

        const string filename = "NE0260034056__Application New Issued Licence 10.09.2020 11497061.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var records = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(10, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(32, additionalInformation.Text!.Count);
        
        var furtherConditions = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "FurtherConditions");
        Assert.NotNull(furtherConditions);
        Assert.Equal("9. FURTHER CONDITIONS", furtherConditions.Text?.FirstOrDefault()?.Text);
        Assert.Equal(38, furtherConditions.Text!.Count);

        Assert.Equal(4, furtherConditions.SubResults.Count);
        Assert.Equal("9.1 (i) No abstraction shall take place unless the Licence Holder has installed a", furtherConditions.SubResults[0].Text!.First().Text);

        Assert.Equal("9.2 No abstraction shall take place when the flow in the Back Delfin as gauged", furtherConditions.SubResults[1].Text!.First().Text);
        Assert.Equal("NE/026/0034/052", furtherConditions.SubResults[1].SubResults[0].Text!.First().Text);
        Assert.Equal("NE/026/0034/053", furtherConditions.SubResults[1].SubResults[1].Text!.First().Text);
        
        Assert.Equal("9.3 Abstraction shall not exceed 2,000 cubic metres per day when the flow in the", furtherConditions.SubResults[2].Text!.First().Text);
        Assert.Equal("NE/026/0034/052", furtherConditions.SubResults[2].SubResults[0].Text!.First().Text);
        Assert.Equal("NE/026/0034/053", furtherConditions.SubResults[2].SubResults[1].Text!.First().Text);
        
        Assert.Equal("9.4 The minimum value for the quantity of water authorised to be abstracted", furtherConditions.SubResults[3].Text!.First().Text);

        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, 1, TestConfig.PdfFolder2),
            AbsLicCacheService);
        
        Assert.Equal(3, licenceSets.Count);
        var agreedSchemaLicenceGroup = licenceSets[1];
        
        Assert.Equal("NE0260034018-LV2019121120250331-NE0260034052-LV2019121120270331-NE0260034053-LVUNKNOWN-NE0260034056-LV2020091020370331",
            agreedSchemaLicenceGroup.LicenceSetId);
        
        //Assert.Equal(4, agreedSchemaLicenceGroup.AggregateSets!.Length);
        Assert.Equal(4, agreedSchemaLicenceGroup.Licences.Length);
        
        // For primary licence
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];
        
        Assert.Equal("NE/026/0034/056", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(ScrapeStatus.Ok, agreedSchemaLicence.Status);

        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual!.Length);
        Assert.Equal(5, agreedSchemaLicence.AbstractionLimits.Individual.SelectMany(x => x.Limits).Count());
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Aggregates.SelectMany(x => x.Limits).Count());
        
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal(InformationSource.Document, agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].Source);
        
        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        Assert.Equal(InformationSource.Document, agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].Source);
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);        
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);        
        
        // For second licence
        agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[1];
        
        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(ScrapeStatus.Ok, agreedSchemaLicence.Status);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual!);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Equal(4, agreedSchemaLicence.AbstractionLimits.Individual!.SelectMany(x => x.Limits).Count());
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);

        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal(InformationSource.Document, agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].Source);
        Assert.Equal(InformationDirection.Incoming, agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal(InformationSource.OtherDocument, agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].Source);
        Assert.Equal(InformationDirection.Incoming, agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].Direction);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("ReadInConjunction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].LinkReason);
        Assert.Equal(InformationSource.OtherDocument, agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].Source);
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        Assert.Equal("NE/026/0034/056", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal(InformationDirection.Incoming, agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].Direction);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition"
            , agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
        
        // For third licence
        agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[2];
        
        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(ScrapeStatus.Ok, agreedSchemaLicence.Status);
        
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Individual!);
        Assert.Equal(8, agreedSchemaLicence.AbstractionLimits.Aggregates!.Length);
        Assert.Equal(22, agreedSchemaLicence.AbstractionLimits.Aggregates!.SelectMany(x => x.Limits).Count());

        Assert.Equal(4, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/027/0028/059", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("AggregateConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].LinkReason);
        
        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        Assert.Equal(InformationSource.Document, agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].Source);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[1].ContainedIn![1].SectionName);
        Assert.Equal("ReadInConjunction", agreedSchemaLicence.LinkedLicences[1].ContainedIn![1].LinkReason);
        Assert.Equal(InformationSource.Document, agreedSchemaLicence.LinkedLicences[1].ContainedIn![1].Source);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![2].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![2].LinkReason);
        Assert.Equal(InformationSource.OtherDocument, agreedSchemaLicence.LinkedLicences[1].ContainedIn![2].Source);        
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("AggregateConditions", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
        
        Assert.Equal("NE/026/0034/056", agreedSchemaLicence.LinkedLicences[3].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[3].ContainedIn!);
        Assert.Equal(InformationDirection.Incoming, agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].Direction);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].SectionName);
        Assert.Equal(InformationSource.OtherDocument, agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].Source);
        Assert.Equal("SimultaneousDischargeCondition",
            agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].LinkReason);   
        
        // For fourth licence
        agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[3];
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(ScrapeStatus.NotFound, agreedSchemaLicence.Status);
        Assert.Empty(agreedSchemaLicence.AbstractionLimits.Individual!);
    }
    
    [Fact]
    public async Task When_GettingRecords_ShouldFindOne()
    {
        // Arrange

        const string filename = "22718033__Application - Minor Variation - Issued Licence - 16022023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var records = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(12, records.Text!.Count);

        Assert.Equal(3, records.SubResults.Count);
        Assert.Equal("8.1 The Licence Holder shall take and record readings of the meter specified in", records.SubResults[0].Text!.FirstOrDefault()!.Text);
        Assert.Equal("2/27/18/158/R01", records.SubResults[0].SubResults[0].Text!.FirstOrDefault()!.Text);
        Assert.Equal("2/27/18/117/R01", records.SubResults[0].SubResults[1].Text!.FirstOrDefault()!.Text);
        
        Assert.Equal("8.2 The Licence Holder shall send a copy of the record or summary data from it to", records.SubResults[1].Text!.FirstOrDefault()!.Text);
        Assert.Equal("8.3 Each record shall be kept and be made available during all reasonable", records.SubResults[2].Text!.FirstOrDefault()!.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, 2, TestConfig.PdfFolder2),
            AbsLicCacheService)).Last();
        
        Assert.Equal(4, agreedSchemaLicenceGroup.Licences.Length);
        
        Assert.Equal("2/27/18/033", agreedSchemaLicenceGroup.Licences[0].LicenceNumber?.Value);
        Assert.Equal("2/27/18/158/R01", agreedSchemaLicenceGroup.Licences[1].LicenceNumber?.Value);
        Assert.Equal("2/27/18/117/R01", agreedSchemaLicenceGroup.Licences[2].LicenceNumber?.Value);
        Assert.Equal("NE/027/0018/041", agreedSchemaLicenceGroup.Licences[3].LicenceNumber?.Value);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal("2/27/18/033", agreedSchemaLicence.LicenceNumber?.Value);
        
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("2/27/18/158/R01", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("Records", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("ReadingsDischargedAugmentationCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("SimultaneousCompensatoryDischargeCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        
        Assert.Equal("2/27/18/117/R01", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal("Records", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("ReadingsDischargedAugmentationCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![1].SectionName);
        Assert.Equal("SimultaneousCompensatoryDischargeCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![1].LinkReason);
        
        Assert.Equal("NE/027/0018/041", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousCompensatoryDischargeCondition", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task When_BackLinkX_ThenNowGetsThem()
    {
        // Arrange
        const string filename = "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, 1, TestConfig.PdfFolder2),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("NE0260034018-LV2019121120250331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var expectedLicenceSetId =
            "NE0260034018-LV2019121120250331-NE0260034052-LV2019121120270331-NE0260034053-LVUNKNOWN";
        Assert.Equal(expectedLicenceSetId, licenceSets[1].LicenceSetId);
        Assert.Equal([LicenceSetType.AllLicencesExplicitlyReferencedAnywhere], licenceSets[1].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[1];
        Assert.Equal(expectedLicenceSetId, agreedSchemaLicenceGroup.LicenceSetId);

        Assert.Single(agreedSchemaLicenceGroup.AggregateSets!);
        Assert.Equal("NE0260034052-LV2019121120270331", agreedSchemaLicenceGroup.AggregateSets![0].AggregateSetId);
        
        Assert.Equal(3, agreedSchemaLicenceGroup.Licences.Length); // TODO should have a /056 back link ideally
        
        // For primary licence
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal(2, agreedSchemaLicence.LicenceSets.Length);
        Assert.Equal("NE0260034018-LV2019121120250331", agreedSchemaLicence.LicenceSets[0].LicenceSetId);
        Assert.Equal(expectedLicenceSetId, agreedSchemaLicence.LicenceSets[1].LicenceSetId);
        
        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(ScrapeStatus.Ok, agreedSchemaLicence.Status);
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual!);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates!);
        
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal(InformationDirection.Incoming, agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("ReadInConjunction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].LinkReason);
        Assert.Equal(InformationDirection.Incoming, agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].Direction);

        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        // For second licence
        agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[1];
        
        Assert.Equal(2, agreedSchemaLicence.LicenceSets.Length);
        Assert.Equal("NE0260034018-LV2019121120250331", agreedSchemaLicence.LicenceSets[0].LicenceSetId);
        Assert.Equal(expectedLicenceSetId, agreedSchemaLicence.LicenceSets[1].LicenceSetId);
        
        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(ScrapeStatus.Ok, agreedSchemaLicence.Status);
        
        Assert.NotNull(agreedSchemaLicence.Points);
        Assert.Equal(4, agreedSchemaLicence.Points.Length);
        
        Assert.NotNull(agreedSchemaLicence.Purposes);
        Assert.Equal(3, agreedSchemaLicence.Purposes.Length);
        
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Individual!);
        Assert.Equal(22, agreedSchemaLicence.AbstractionLimits.Aggregates!.SelectMany(x => x.Limits).Count());
        
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/027/0028/059", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal(InformationSource.Document, agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].Source);
        
        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[1].ContainedIn![1].SectionName);
        Assert.Equal("ReadInConjunction", agreedSchemaLicence.LinkedLicences[1].ContainedIn![1].LinkReason);
        Assert.Equal(InformationDirection.Incoming, agreedSchemaLicence.LinkedLicences[1].ContainedIn![2].Direction);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![2].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![2].LinkReason);
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("AggregateConditions", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
        Assert.Equal(InformationSource.Document, agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].Source);
        
        // For third licence
        agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[2];
        
        Assert.Equal(2, agreedSchemaLicence.LicenceSets.Length);
        Assert.Equal("NE0260034018-LV2019121120250331", agreedSchemaLicence.LicenceSets[0].LicenceSetId);
        Assert.Equal(expectedLicenceSetId, agreedSchemaLicence.LicenceSets[1].LicenceSetId);
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(ScrapeStatus.NotFound, agreedSchemaLicence.Status);
    }
    
    [Fact]
    public async Task When_XBackLinkX_ThenNowGetsThem()
    {
        // Arrange
        const string filename = "NE0270023043__Application New Licence Issued 18.12.2018 10623801.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 2, TestConfig.PdfFolder2),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("NE0270023043-LV2018121720290331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];
        
        Assert.Equal("NE/027/0023/043", agreedSchemaLicence.LicenceNumber?.Value);        

        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual!.Length);
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Individual![0].Limits.Count);
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Individual![1].Limits.Count);
        
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates!);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits);
        Assert.Equal(16005, agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[0].Value);
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits[0].Units);
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Aggregates![0].Purposes);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates![0].Purposes!.Length);
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_LookingForCorrectDefinitionOfYear()
    {
        // Arrange

        const string filename = "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);

        Assert.NotNull(resultFull.Matches?.FirstOrDefault(m => m.LabelGroupName == "Purposes"));
        Assert.NotNull(resultFull.Matches?.FirstOrDefault(m => m.LabelGroupName == "Additional"));
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, 1, TestConfig.PdfFolder2),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("NE0260034018-LV2019121120250331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var aggregateLicenceSet = licenceSets[1];
        var otherLicence = aggregateLicenceSet.Licences[1];
        Assert.Equal("NE/026/0034/052", otherLicence.LicenceNumber!.Value);
        Assert.Equal(ScrapeStatus.Ok, otherLicence.Status);
        
        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("1 April", agreedSchemaLicence.DefinitionOfYear!.StartDate);
        Assert.Equal("31 March", agreedSchemaLicence.DefinitionOfYear.EndDate);
        
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length);

        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal(InformationDirection.Outgoing, agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal(InformationDirection.Incoming, agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("ReadInConjunction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].LinkReason);
        Assert.Equal(InformationDirection.Incoming, agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].Direction);
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.AbstractionLimits!.Individual!);
    }
    
    [Fact]
    public async Task When_FindingAdditionalInformationExtraReason()
    {
        // Arrange

        const string filename = "12100074R01__Application WR Abstraction Licence Issued 11042025.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 3, 3);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("12100074R01-LV2025040920270331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("1/21/00/074/R01", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Equal("01 April", agreedSchemaLicence.DefinitionOfYear!.StartDate);
        Assert.Equal("31 March", agreedSchemaLicence.DefinitionOfYear.EndDate);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);

        Assert.Equal("1/21/00/073/R01", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("UsedInConjunction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].LinkReason);        
    }
    
    [Fact]
    public async Task When_FindingAdditionalInformationExtraReason2()
    {
        // Arrange

        const string filename = "12100073R01__Application - New -  Issued Licence 31.03.2015 8814302.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("12100073R01-LV2015040120270331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("1/21/00/073/R01", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear); // TODO - why?
        Assert.Single(agreedSchemaLicence.LinkedLicences);

        Assert.Equal("1/21/00/074/R01", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("UsedInConjunction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
    }
    
    [Fact]
    public async Task When_FindingFutherConditionsExtraReason()
    {
        // Arrange

        const string filename = "12100068__Application Normal Variation Licence Issued 17062025.docx.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("12100068-LV20250617", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("1/21/00/068", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Equal("01 April", agreedSchemaLicence.DefinitionOfYear!.StartDate);
        Assert.Equal("31 March", agreedSchemaLicence.DefinitionOfYear.EndDate);
        
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);

        Assert.Equal("1/21/00/001", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("EmergencyCircumstances", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        
        Assert.Equal("1/21/00/069", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("EmergencyCircumstances", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        Assert.Equal("1/21/00/072", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("EmergencyCircumstances", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task When_FindingRecordsExtraReason()
    {
        // Arrange

        const string filename = "22631079__Application – Transfer – Issued Licence – 240223.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("22631079-LV20230224", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/26/31/079", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_FindingRecordsExtraReason4()
    {
        // Arrange

        const string filename = "12304073__Application –  New – Issued licence – November  2015 9083023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("12304073-LV20151106", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("1/23/04/073", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear);
        Assert.Single(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_FindingRecordsExtraReason5()
    {
        // Arrange

        const string filename = "12504142__Application Minor Variation Issued Licence - 27052025.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(13, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("12504142-LV20250423", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("1/25/04/142", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenA_FindingRecordsExtraReason5()
    {
        // Arrange

        const string filename = "12504008__Application - Minor Variation - Issued Licence PDF Copy 9211405.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(13, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("12504008-LV20160203", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("1/25/04/008", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear);
        Assert.Equal(10, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.Equal("1/25/04/009", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        
        Assert.Equal("1/25/05/044", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        Assert.Equal("1/25/04/141", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
        
        Assert.Equal("1/25/04/138", agreedSchemaLicence.LinkedLicences[3].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[3].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].LinkReason);
        
        Assert.Equal("1/25/04/128", agreedSchemaLicence.LinkedLicences[4].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[4].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[4].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", agreedSchemaLicence.LinkedLicences[4].ContainedIn![0].LinkReason);
        
        Assert.Equal("1/25/04/124", agreedSchemaLicence.LinkedLicences[5].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[5].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[5].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", agreedSchemaLicence.LinkedLicences[5].ContainedIn![0].LinkReason);
        
        Assert.Equal("1/25/04/125", agreedSchemaLicence.LinkedLicences[6].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[6].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[6].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", agreedSchemaLicence.LinkedLicences[6].ContainedIn![0].LinkReason);
        
        Assert.Equal("1/25//04/118", agreedSchemaLicence.LinkedLicences[7].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[7].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[7].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", agreedSchemaLicence.LinkedLicences[7].ContainedIn![0].LinkReason);
        
        Assert.Equal("1/25/03/031/R01", agreedSchemaLicence.LinkedLicences[8].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[8].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[8].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", agreedSchemaLicence.LinkedLicences[8].ContainedIn![0].LinkReason);
        
        Assert.Equal("1/25/04/107", agreedSchemaLicence.LinkedLicences[9].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[9].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[9].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", agreedSchemaLicence.LinkedLicences[9].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task WhenB_FindingRecordsExtraReason5()
    {
        // Arrange

        const string filename = "22702039__Application Formal Variation Issue Licence 30062023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(3, licenceSets.Count);
        
        Assert.Equal("22702039-LV2023063020370331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/27/18/153/R01", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        Assert.Equal("2/27/02/039", agreedSchemaLicence.LicenceNumber?.Value);
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Equal(18, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.Equal("2/27/09/025", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        
        Assert.Equal("NE/027/0018/009", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        Assert.Equal("NE/027/0018/033", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
        
        Assert.Equal("2/27/18/053", agreedSchemaLicence.LinkedLicences[3].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[3].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[4].ContainedIn!);
        Assert.Equal("2/27/18/131/R01", agreedSchemaLicence.LinkedLicences[4].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[4].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[4].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[5].ContainedIn!);
        Assert.Equal("2/27/18/146/R01", agreedSchemaLicence.LinkedLicences[5].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[5].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[5].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[6].ContainedIn!);
        Assert.Equal("2/27/18/147/R01", agreedSchemaLicence.LinkedLicences[6].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[6].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[6].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[7].ContainedIn!);
        Assert.Equal("2/27/18/152/R01", agreedSchemaLicence.LinkedLicences[7].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[7].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[7].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[8].ContainedIn!);
        Assert.Equal("2/27/18/158/R01", agreedSchemaLicence.LinkedLicences[8].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[8].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[8].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[9].ContainedIn!);
        Assert.Equal("NE/027/0024/003/R01", agreedSchemaLicence.LinkedLicences[9].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[9].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[9].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[10].ContainedIn!);
        Assert.Equal("NE/027/0024/071", agreedSchemaLicence.LinkedLicences[10].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[10].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[10].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[11].ContainedIn!);
        Assert.Equal("2/27/24/477/R01", agreedSchemaLicence.LinkedLicences[11].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[11].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[11].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[12].ContainedIn!);
        Assert.Equal("2/27/24/478/R01", agreedSchemaLicence.LinkedLicences[12].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[12].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[12].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[13].ContainedIn!);
        Assert.Equal("2/27/24/479/R01", agreedSchemaLicence.LinkedLicences[13].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[13].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[13].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[14].ContainedIn!);
        Assert.Equal("2/27/24/480/R01", agreedSchemaLicence.LinkedLicences[14].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[14].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[14].ContainedIn![0].LinkReason);

        Assert.Single(agreedSchemaLicence.LinkedLicences[15].ContainedIn!);
        Assert.Equal("2/27/24/486/R01", agreedSchemaLicence.LinkedLicences[15].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[15].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[15].ContainedIn![0].LinkReason);      
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[16].ContainedIn!);
        Assert.Equal("NE/027/0028/029/R01", agreedSchemaLicence.LinkedLicences[16].LicenceNumber);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[16].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[16].ContainedIn![0].LinkReason);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences[17].ContainedIn!);
        Assert.Equal("NE/027/0018/037", agreedSchemaLicence.LinkedLicences[17].LicenceNumber);
        Assert.Equal("Additional", agreedSchemaLicence.LinkedLicences[17].ContainedIn![0].SectionName);
        Assert.Equal("DonorLicence", agreedSchemaLicence.LinkedLicences[17].ContainedIn![0].LinkReason);
    }

    [Fact]
    public async Task WhenZaa_A()
    {
        // Arrange

        const string filename = "22727153__Application Formal Variation Issued Licence - 14122023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 5);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(3, licenceSets.Count);
        
        Assert.Equal("22727153-LV20231214", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/27/27/153", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        Assert.Equal("2/27/27/153", agreedSchemaLicence.LicenceNumber!.Value);
        
        Assert.Equal(4, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/027/0027/017/R01", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("NE/027/0027/018/R01", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal("NE/027/0027/047", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Equal("NE/027/027/018/R01", agreedSchemaLicence.LinkedLicences[3].LicenceNumber);
    }
    
    [Fact]
    public async Task WhenZ_A()
    {
        // Arrange

        const string filename = "22722265__Application - new - issue licence 9393610.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("22722265-LV20160630", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/27/22/265", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        
        Assert.Equal("NE/027/0022/043", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
    }
    
    [Fact]
    public async Task WhenZ_C()
    {
        // Arrange

        const string filename = "NE0270024056__Application Formal Variation Issued Licence - [11072017] - (11072017).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("NE0270024056-LV2017061220300331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("NE/027/0024/056", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length); // TODO this is because of the space in the file
        
        Assert.Equal("NE/027/0024/049", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("NE/027/0024/066", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);        
    }
    
    [Fact]
    public async Task WhenZ_D()
    {
        // Arrange

        const string filename = "22708052__Application - Formal Variation - Issued Licence 24.01.2017 9644004.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("22708052-LV20170124", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/27/08/052", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal("2/27/08/052", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.Equal("2/27/08/144/R01", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("2/27/08/144", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal("NE/027/0008/017", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
    }
    
    [Fact]
    public async Task WhenZ_E()
    {
        // Arrange

        const string filename = "22728270R01__Application - New - Issued Licence 24.06.2015 8918352.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("22728270R01-LV2015062420270331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/27/28/270/R01", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);

        Assert.Equal("2/27/28/270", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("2/27/28/231", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal("2/27/28/083", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
    }
    
    [Fact]
    public async Task WhenZ_F()
    {
        // Arrange

        const string filename = "ne0230003031__Application – NA New – Issued Licence-22072022.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("NE0230003031-LV2022072220300331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("NE/023/0003/031", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        
        Assert.Equal("NE/023/0003/030", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
    }
    
    [Fact]
    public async Task WhenZ_G()
    {
        // Arrange

        const string filename = "NE0210000014__Application NA New Issued Licence 31-03-2021 11765884.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("NE0210000014-LV2021033120270331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("NE/021/0000/014", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenZ_H()
    {
        // Arrange

        const string filename = "NE0270024044__Application Variation Issued Licence June 2017.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);

        var linkedLicences = resultFull.Matches!
            .Where(x => x.LabelGroupName == "LinkedLicenceNumber")
            .ToList();
        
        Assert.Equal(4, linkedLicences.Count);
        Assert.Equal("MD/028/0084/008", linkedLicences[0].Text?.FirstOrDefault()?.Text);
        Assert.Equal("2/27/24/034", linkedLicences[1].Text?.FirstOrDefault()?.Text);
        Assert.Equal("9.2.2", linkedLicences[2].Text?.FirstOrDefault()?.Text); // TODO this shouldnt be here
        Assert.Equal("NE/027/0024/044", linkedLicences[3].Text?.FirstOrDefault()?.Text);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(3, licenceSets.Count);
        
        Assert.Equal("NE0270024044-LV2017061320290331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("NE/027/0024/044", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.Equal("MD/028/0084/008", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);        
        Assert.Equal("2/27/24/034", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
    }
    
    [Fact]
    public async Task WhenZ_I()
    {
        // Arrange

        const string filename = "NE0240005016__Application - Formal Variation -Application New Licence Issued 24_03_2021 00_00_00 11751498.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(13, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("NE0240005016-LV2021032420260331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("NE/024/0005/016", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenZ_J()
    {
        // Arrange

        const string filename = "NE0230001007__Application – NA New – Issued Licence-22072022.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("NE0230001007-LV2022072220300331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("NE/023/0001/007", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenZ_K()
    {
        // Arrange

        const string filename = "22722562R01__Application - Minor Variation -Application New Licence Issued 25_06_2019 00_00_00 10900765.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("22722562R01-LV2019052220290331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/27/22/562/R01", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenZ_M()
    {
        // Arrange

        const string filename = "NE0240005010__Application - New HEP Licence - Issued Licence 6 June 2013 7844848.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("NE0240005010-LV20130606", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("NE/024/0005/010", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenZ_N()
    {
        // Arrange

        const string filename = "22724199__Drax licence document - Amended 6065605.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("22724199-LV20100716", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/27/24/199", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenZ_O()
    {
        // Arrange

        const string filename = "NE0270007008__Application New Issued Licence 31.03.2014 8288333.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Single(licenceSets);
        
        Assert.Equal("NE0270007008-LV2014033120360331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("NE/027/0007/008", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task FileWithALotOfLinkedLicenceAggregates()
    {
        // Arrange

        const string filename = "22718077__Application - Minor Variation - Issued Licence 25.10.2016 9535704.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("22718077-LV20161012", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/27/18/077", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Equal(6, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual.Length);
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits.Count);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[1].Limits);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates.Length);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits.Count);
        Assert.Equal(6, agreedSchemaLicence.AbstractionLimits.Aggregates[0].LinkedLicences?.Length);
    }

    [Fact]
    public async Task WeirdLineWrapping()
    {
        // Arrange
        const string filename = "22715041__Application Formal Variation Issued Licence - 22032013.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 5);
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(5, 3, TestConfig.PdfFolder3),
            AbsLicCacheService);
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("22715041-LV20130322", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/27/15/041", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());

        Assert.NotNull(agreedSchemaLicence.Points);
        Assert.Single(agreedSchemaLicence.Points);
        Assert.Equal("1 Inland water (reservoir) known as Watersheddles Reservoir at Keighley, West Yorkshire", agreedSchemaLicence.Points[0].Description1); // TODO shouldnt have the 1
        
        Assert.Null(agreedSchemaLicence.DefinitionOfYear);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual.Length);

        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits.Count);
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual[0].TimeCutoff);        
        Assert.Equal("31 March 2027", agreedSchemaLicence.AbstractionLimits.Individual[0].TimeCutoff!.Date);
        Assert.Equal(CutoffType.Upto, agreedSchemaLicence.AbstractionLimits.Individual[0].TimeCutoff!.CutoffType);
        
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual[1].Limits);
        Assert.Equal("1 April 2027", agreedSchemaLicence.AbstractionLimits.Individual[1].TimeCutoff!.Date);
        Assert.Equal(CutoffType.From, agreedSchemaLicence.AbstractionLimits.Individual[1].TimeCutoff!.CutoffType);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits.Count);
        Assert.Equal("31 March 2027", agreedSchemaLicence.AbstractionLimits.Aggregates[0].TimeCutoff!.Date);
        Assert.Equal(CutoffType.Upto, agreedSchemaLicence.AbstractionLimits.Aggregates[0].TimeCutoff!.CutoffType);
        
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Aggregates[0].LinkedLicences?.Length);
        Assert.Equal("2/27/14/009", agreedSchemaLicence.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("2/27/14/010", agreedSchemaLicence.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.Equal("2/27/14/058", agreedSchemaLicence.AbstractionLimits.Aggregates[0].LinkedLicences![2]);
    }
}