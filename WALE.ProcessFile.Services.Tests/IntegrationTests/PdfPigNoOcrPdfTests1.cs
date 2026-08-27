using System.Text.Json;
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
using WRADI.DocumentType.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Services;
using WRADI.Services.Cache.AbstractionLicence;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[Collection("PdfPigNoOcrPdfTests1 Collection")]
[EnableParallelization]
public class PdfPigNoOcrPdfTests1(StandaloneFixture1 fixture)
{
    private static readonly ICacheService CacheService;
    private static readonly IAbstractionLicenceCacheService AbsLicCacheService;
    
    private static readonly FileSystemCacheService? RealCacheService;
    private static readonly FileSystemAbstractionLicenceCacheService? RealAbsLicCacheService;
    
    static PdfPigNoOcrPdfTests1()
    {
        RealCacheService = new FileSystemCacheService("Cache/");
        RealAbsLicCacheService = new FileSystemAbstractionLicenceCacheService("Cache/");

        (CacheService, AbsLicCacheService) = GeneralTestsHelper.GetFakeCacheService(
            RealCacheService,
            RealAbsLicCacheService,
            NaldData,
            [],
            FileLicenceMapping);
        
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
    public async Task WhenX_NotCheckingAbstractionLimits_ThenFoundCorrectly_IncludesAgreedSchema()
    {
        // Arrange

        const string filename = "Application –Transfer– Issued Licence –05072022.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var history = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceHistory");
        Assert.NotNull(history);
        Assert.Equal(14, history.Text!.Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(10, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(14, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Ingleby Greenhow Water Society Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal("CompanyName3", nameResult.MatchedLabel!.Name);
        Assert.Equal("CompanyName3", nameResult.MatchedLabelName);
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        Assert.Equal(6, nameResult.LabelStartLineNumber);
        
        // Note no other licence mentioned
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(5, abstractionLimitsSection.Text!.Count);
        Assert.Equal("A day means any period of 24 consecutive hours and a year means the", abstractionLimitsSection.Text![3].Text);
        Assert.Equal(15, abstractionLimitsSection.LabelStartLineNumber);
        
        Assert.NotNull(abstractionLimitsSection.SubResults);
        Assert.Single(abstractionLimitsSection.SubResults);
        
        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint1.SubResults);

        var point1Sub1 = abstractionLimitsPoint1.SubResults[0];
        Assert.NotNull(point1Sub1);
        Assert.Equal("AbstractionLimitPointSub", point1Sub1.MatchedLabel?.Name);
        
        Assert.Equal(4, point1Sub1.Text!.Count);

        Assert.NotNull(point1Sub1.SubResults);
        Assert.Equal(6, point1Sub1.SubResults.Count);

        var perDay = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(16, perDay.LabelStartLineNumber);
        Assert.Equal("90.91", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        var perYear = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("33182", perYear);
        
        var perYearUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("1/25/04/059", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(0, licenceNumberResult.LabelStartLineNumber);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal("4. PURPOSES OF ABSTRACTION 4.1 Private Water Supply. 4.2 Agriculture (other than Spray Irrigation).",
            string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!));
        Assert.Equal(["PURPOSES OF ABSTRACTION"], purposeResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.BetweenLabels, purposeResult.MatchedPosition);
        
        Assert.Single(purposeResult.SubResults);
        
        var firstPurposePointGroup = purposeResult.SubResults.First();
        var firstPurpose = firstPurposePointGroup.SubResults[0];
        
        Assert.Equal("Purposes", firstPurpose.MatchedLabel!.Name);
        Assert.Equal("4.1 Private Water Supply.", firstPurpose.Text!.First().Text);
        Assert.Equal(2, firstPurpose.SubResults.Count);
        
        var firstPurposeWithoutPrepoint = firstPurpose.SubResults[1];
        Assert.Equal("Private Water Supply", firstPurposeWithoutPrepoint.Text!.First().Text);
        
        var secondPurpose = firstPurposePointGroup.SubResults[1];
        Assert.Equal("4.2 Agriculture (other than Spray Irrigation).", secondPurpose.Text!.First().Text);        
        
        var secondPurposeWithoutPrepoint = secondPurpose.SubResults[1];
        Assert.Equal("Agriculture (other than Spray Irrigation)", secondPurposeWithoutPrepoint.Text!.First().Text);

        var agreedSchemaLicenceGroup = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService, NaldDataLookupService);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.Single();

        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("1/25/04/059", agreedSchemaLicence.LicenceNumber?.Value);
        
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual![0].Limits.Count);

        var limitG = agreedSchemaLicence.AbstractionLimits.Individual[0];
        var limit = limitG.Limits[0];
        
        Assert.Equal(LimitPeriodType.PerDay, limit.PeriodType);
        Assert.Equal("cubic metres", limit.Units);
        Assert.Equal(90.91, limit.Value);
        Assert.Null(limit.Points);
        Assert.Equal(1, limitG.Points.Length);
        Assert.Equal(0, limitG.Points.Count(c => c.IsImplicit != true));
        Assert.Equal(2, limitG.Purposes.Length);
        Assert.Equal(0, limitG.Purposes.Count(c => c.IsImplicit != true));
        
        limit = limitG.Limits[1];
        Assert.Equal(LimitPeriodType.PerYear, limit.PeriodType);
        Assert.Equal("cubic metres", limit.Units);
        Assert.Equal(33182, limit.Value);
        Assert.Null(limit.Points);        
        Assert.Equal(1, limitG.Points.Length);
        Assert.Equal(0, limitG.Points.Count(c => c.IsImplicit != true));
        Assert.Equal(2, limitG.Purposes.Length);
        Assert.Equal(0, limitG.Purposes.Count(c => c.IsImplicit != true));
        
        Assert.NotNull(agreedSchemaLicence.LicenceVersion);
        Assert.Equal("LV20220705", agreedSchemaLicence.LicenceVersion.LicenceVersionId);
        
        Assert.Null(agreedSchemaLicence.LicenceVersion.ExpiryDate);
        Assert.Equal(new DateTime(2022, 07, 05), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Equal(new DateTime(1968, 05, 15), agreedSchemaLicence.LicenceVersion.OriginalIssueDate);
        Assert.Equal(new DateTime(2022, 07, 05), agreedSchemaLicence.LicenceVersion.IssueDate);

        Assert.Equal("12504059-LV20220705", agreedSchemaLicenceGroup.Last().LicenceSetId);
        
        Assert.NotNull(agreedSchemaLicenceGroup.Last().Licences);
        Assert.Single(agreedSchemaLicenceGroup.Last().Licences);

        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Null(agreedSchemaLicenceGroup.Last().AggregateSets);
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task LongLicenceHolderName_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application - Minor Variation -Application New Licence Issued 24_12_2019 00_00_00 11164372.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var history = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceHistory");
        Assert.NotNull(history);
        Assert.Equal(2, history.Text!.Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(8, records.Text!.Count);
        
        var additionalInformation = resultList.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(9, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Lady Isabelle Jacqueline Laline Hay, Countess of Erroll, Sir Thomas Minshull Stockdale, 2nd Baronet Stockdale, Robert Elkington",
            string.Join(", ", nameResult.Text!.Select(x => x.Text)));
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);

        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/0422", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.False(abstractionLimitsResult.IsOcr);
        Assert.Equal(7, abstractionLimitsResult.Text!.Count);
        Assert.Equal(17, abstractionLimitsResult.LabelStartLineNumber);
        
        Assert.NotNull(abstractionLimitsResult.SubResults);        
        Assert.Equal(2, abstractionLimitsResult.SubResults.Count);
        Assert.Equal(17, abstractionLimitsResult.LabelStartLineNumber);
        
        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal(2, abstractionLimitsSection1.Text!.Count);

        Assert.NotNull(abstractionLimitsSection1.SubResults);
        
        Assert.Single(abstractionLimitsSection1.SubResults);
        var section1Sub1 = abstractionLimitsSection1.SubResults[0];
        Assert.Equal(5, section1Sub1.SubResults.Count);

        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(19, perDay.LabelStartLineNumber);
        Assert.Equal("205", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perHour = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("41", perHour);
        
        var perHourUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsResult.SubResults[1];
        Assert.Equal(4, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        
        Assert.Single(abstractionLimitsSection2.SubResults);
        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        
        Assert.Equal(7, section2Sub1.SubResults.Count);  
        
        var perYear1 = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("6138", perYear1);
        
        var perYearUnits1 = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits1);
        
        var perYear2 = section2Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("6138", perYear2);
        
        var perYearUnits2 = section2Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits2);        

        var pointsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.Equal(4, pointsResult?.Text!.Count);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal("4. PURPOSE OF ABSTRACTION", purposeResult.Text?[0].Text);
        Assert.Equal("4.1 Spray irrigation (other than spray irrigation under glass).", purposeResult.Text?[1].Text);
        Assert.Equal(["PURPOSE OF ABSTRACTION"], purposeResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.BetweenLabels, purposeResult.MatchedPosition);
        
        Assert.Single(purposeResult.SubResults);
        var firstPurposePointGroup = purposeResult.SubResults.Single();
        Assert.Equal("4.1 Spray irrigation (other than spray irrigation under glass).", firstPurposePointGroup.Text!.Single().Text);

        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService, NaldDataLookupService)).Last();
        
        Assert.Equal("2839220338-LVUNKNOWN-2839220422-LV20191111", agreedSchemaLicenceGroup.LicenceSetId);
        
        Assert.NotNull(agreedSchemaLicenceGroup.Licences);
        Assert.Equal(2, agreedSchemaLicenceGroup.Licences.Length);
        
        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Equal(filename, primaryLicence.Filename);
        Assert.Equal("28/39/22/0422", primaryLicence.LicenceNumber?.Value);

        Assert.Equal(2, primaryLicence.AbstractionLimits.Individual![0].Limits.Count);

        var limitG = primaryLicence.AbstractionLimits.Individual[0];
        var limit = limitG.Limits[0];
        
        Assert.Equal(LimitPeriodType.PerHour, limit.PeriodType);
        Assert.Equal("cubic metres", limit.Units);
        Assert.Equal(41, limit.Value);
        Assert.Null(limit.Points);
        Assert.Equal(4, limitG.Points.Length);
        Assert.Equal(0, limitG.Points.Count(c => c.IsImplicit != true));
        Assert.Equal(1, limitG.Purposes.Length);
        Assert.Equal(0, limitG.Purposes.Count(c => c.IsImplicit != true));

        limit = limitG.Limits[1];
        Assert.Equal(LimitPeriodType.PerDay, limit.PeriodType);
        Assert.Equal("cubic metres", limit.Units);
        Assert.Equal(205, limit.Value);
        Assert.Null(limit.Points);
        Assert.Equal(4, limitG.Points.Length);
        Assert.Equal(0, limitG.Points.Count(c => c.IsImplicit != true));
        Assert.Equal(1, limitG.Purposes.Length);
        Assert.Equal(0, limitG.Purposes.Count(c => c.IsImplicit != true));
        
        Assert.NotNull(agreedSchemaLicenceGroup.AggregateSets);
        Assert.Single(agreedSchemaLicenceGroup.AggregateSets);

        Assert.NotNull(agreedSchemaLicenceGroup.AggregateSets[0].Aggregates);
        Assert.Single(agreedSchemaLicenceGroup.AggregateSets[0].Aggregates);
        Assert.Equal("2839220338-LVUNKNOWN-2839220422-LV20191111", agreedSchemaLicenceGroup.AggregateSets[0].AggregateSetId);
        
        Assert.Single(primaryLicence.AbstractionLimits.Aggregates!);
        Assert.Single(primaryLicence.AbstractionLimits.Aggregates![0].Limits);
        
        var aggregate = primaryLicence.AbstractionLimits.Aggregates[0];
        Assert.Equal(LimitPeriodType.PerYear, aggregate.Limits[0].PeriodType);
        Assert.Equal("cubic metres", aggregate.Limits[0].Units);
        Assert.Equal(6138, aggregate.Limits[0].Value);  

        Assert.NotNull(primaryLicence.LicenceVersion);
        Assert.Equal("LV20191111", primaryLicence.LicenceVersion.LicenceVersionId);
        
        Assert.Null(primaryLicence.LicenceVersion.ExpiryDate);
        Assert.Equal(new DateTime(2019, 11, 11), primaryLicence.LicenceVersion.EffectiveDate);
        Assert.Equal(new DateTime(1975, 01, 22), primaryLicence.LicenceVersion.OriginalIssueDate);
        Assert.Equal(new DateTime(2019, 12, 24), primaryLicence.LicenceVersion.IssueDate);
        
        Assert.Single(primaryLicence.LinkedLicences);
        Assert.Single(primaryLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }

    [Fact]
    public async Task X_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application – Transfer – Issued Licence – 07.07.2022.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(10, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(41, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("T Wilson & Sons (Farmers)", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("NW/069/0025/091/R01", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.False(abstractionLimitsResult.IsOcr);
        Assert.Equal(17, abstractionLimitsResult.Text!.Count);
        Assert.Equal(17, abstractionLimitsResult.LabelStartLineNumber);
        
        Assert.NotNull(abstractionLimitsResult.SubResults);       
        
        Assert.Equal(2, abstractionLimitsResult.SubResults.Count);
        Assert.Equal(17, abstractionLimitsResult.LabelStartLineNumber);
        
        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal(4, abstractionLimitsSection1.Text!.Count);

        Assert.NotNull(abstractionLimitsSection1.SubResults);
        Assert.Single(abstractionLimitsSection1.SubResults);
        
        var section1Sub1 = abstractionLimitsSection1.SubResults[0];
        
        Assert.Equal(9, section1Sub1.SubResults.Count);
        
        var perHour = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("39.5", perHour);
        
        var perHourUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(19, perDay.LabelStartLineNumber);
        Assert.Equal("948", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perYear = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per year")) == true);

        Assert.NotNull(perYear);
        Assert.Equal(20, perYear.LabelStartLineNumber);
        Assert.Equal("40000", perYear.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsResult.SubResults[1];
        Assert.Equal(12, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(13, section2Sub1.SubResults.Count);
            
        perHour = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("39.5", perHour);
        
        perHourUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(27, perDay.LabelStartLineNumber);
        Assert.Equal("948", perDay.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perYearList = section2Sub1.SubResults
            .Where(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)
            .ToList();
        
       var perYear2 = perYearList.FirstOrDefault()?.Text?.FirstOrDefault()?.Text;
       Assert.Equal("40000", perYear2);
       
       perYear2 = perYearList.LastOrDefault()?.Text?.FirstOrDefault()?.Text;
       Assert.Equal("40000", perYear2); // TODO check value
        
        perYearUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var perSecond = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("10.97", perSecond);
        
        var perSecondUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var linkedLicences = section2Sub1.SubResults
            .Where(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")
            .ToList();
        
        Assert.Equal(2, linkedLicences.Count);
        
        var linkedLicenceNumber1 = linkedLicences[0].Text?[0].Text;
        Assert.Equal("NW/069/0025/006/R01", linkedLicenceNumber1); 
        
        var linkedLicenceNumber2 = linkedLicences[1].Text?[0].Text;
        Assert.Equal("NW/069/0025/007/R01", linkedLicenceNumber2);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal(
            "4. PURPOSE OF ABSTRACTION 4.1 Spray irrigation, subject to the compensatory discharges from the borehole referred to in condition 9.1 below.",
            string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!));
        Assert.Equal(["PURPOSE OF ABSTRACTION"], purposeResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.BetweenLabels, purposeResult.MatchedPosition);
        
        Assert.Single(purposeResult.SubResults);
        var firstPurposePointGroup = purposeResult.SubResults.First();
        Assert.Equal(
            "4.1 Spray irrigation, subject to the compensatory discharges from the borehole referred to in condition 9.1 below.",
            string.Join(' ', firstPurposePointGroup.Text!.Select(x => x.Text).ToArray()));
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService, NaldDataLookupService)).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences[0];
        
        Assert.Equal(3, primaryLicence.LinkedLicences.Length);

        Assert.Equal("NW/069/0025/006/R01", primaryLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, primaryLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("Additional", primaryLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("CompensatoryDischargeCondition", primaryLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        
        Assert.Equal("NW/069/0025/007/R01", primaryLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, primaryLicence.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        Assert.Equal("NW/069/0025/004/R01", primaryLicence.LinkedLicences[2].LicenceNumber);
        Assert.Equal(2, primaryLicence.LinkedLicences[2].ContainedIn!.Length);
        Assert.Equal("FurtherConditions", primaryLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousCompensatoryDischargeCondition", primaryLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task LicenceToCharity_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application new Issued licence 04052017 AN0300012011 9781525.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(12, records.Text!.Count);
        
        var additionalInformation = resultList.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(37, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("The Bourne United Charities", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("AN/030/0012/011", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(12, abstractionLimitsSection.Text!.Count);
        Assert.Equal(27, abstractionLimitsSection.LabelStartLineNumber);
        
        Assert.NotNull(abstractionLimitsSection.SubResults);        
        Assert.Equal(2, abstractionLimitsSection.SubResults.Count);

        var sectionPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(sectionPoint1.SubResults);
        
        var section1Sub1 = sectionPoint1.SubResults[0];
        Assert.Equal(9, section1Sub1.SubResults.Count);
        Assert.Equal(28, section1Sub1.LabelStartLineNumber);
        
        //var abstractionLimitsSection1 = section1Sub1.SubResults[0];
        Assert.Equal(4, section1Sub1.Text!.Count);

        Assert.NotNull(section1Sub1.SubResults);
        Assert.Equal(9, section1Sub1.SubResults.Count);
        
        var perHour = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("55", perHour);
        
        var perHourUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(29, perDay.LabelStartLineNumber);
        Assert.Equal("409.5", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perYear = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per year")) == true);

        Assert.NotNull(perYear);
        Assert.Equal(30, perYear.LabelStartLineNumber);
        Assert.Equal("20457", perYear.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);

        var perSecond = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per second")) == true);

        Assert.NotNull(perSecond);
        Assert.Equal(31, perSecond.LabelStartLineNumber);
        Assert.Equal("15.2", perSecond.Text?.FirstOrDefault()?.Text);
            
        var perSecondUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(7, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        
        var perYear2 = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("22730", perYear2);
        
        perYearUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var linkedLicenceNumber = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("4/30/12/*G/0214", linkedLicenceNumber);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        
        Assert.Equal("4. PURPOSE OF ABSTRACTION 4.1 Spray irrigation, subject to the compensatory discharge of water from the borehole at TF 14084"
            + " 23479 authorised under licence serial number 4/30/12/*G/0214 referred to in Condition 9 below.",
            string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!));
        Assert.Equal(["PURPOSE OF ABSTRACTION"], purposeResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.BetweenLabels, purposeResult.MatchedPosition);
        
        Assert.Single(purposeResult.SubResults);
        var firstPurposePointGroup = purposeResult.SubResults.Single();
        Assert.Equal("4.1 Spray irrigation, subject to the compensatory discharge of water from the borehole at TF 14084"
            + " 23479 authorised under licence serial number 4/30/12/*G/0214 referred to in Condition 9 below.",
            string.Join(' ', firstPurposePointGroup.Text?.Select(x => x.Text).ToArray()!));
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService, NaldDataLookupService)).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences[0];
        
        Assert.Single(primaryLicence.LinkedLicences);

        Assert.Equal("4/30/12/*G/0214", primaryLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(4, primaryLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("Purposes", primaryLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("CompensatoryDischargeCondition", primaryLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal("FurtherConditions", primaryLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("SimultaneousCompensatoryDischargeCondition", primaryLicence.LinkedLicences[0].ContainedIn![2].LinkReason);
        Assert.Equal("Additional", primaryLicence.LinkedLicences[0].ContainedIn![3].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![3].LinkReason);
    }
    
    [Fact]
    public async Task EWPorterAndSon_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application - NA Formal Variation - Issued Licence [26_3_21] 11759321.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(13, records.Text!.Count);
        
        var additionalInformation = resultList.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(28, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("E.W.Porter and Son", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);

        var abstractionLimitsSection = resultList.FirstOrDefault(result =>
            result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(50, abstractionLimitsSection.Text!.Count);
        
        Assert.NotNull(abstractionLimitsSection.SubResults);
        Assert.Equal(10, abstractionLimitsSection.SubResults.Count);
        Assert.Equal(7, abstractionLimitsSection.LabelStartLineNumber);
        
        var point1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(point1.SubResults);
        Assert.Equal(3, point1.Text!.Count);

        var point1Sub1 = point1.SubResults[0];
        Assert.NotNull(point1Sub1.SubResults);
        Assert.Equal(6, point1Sub1.SubResults.Count);

        var abstractionPoint = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "PointCondition")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("Abstraction Point A", abstractionPoint);
        
        var perHour = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("46", perHour);
        
        var perHourUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        var perSecond = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("12.7", perSecond);
        
        var perSecondUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(3, abstractionLimitsSection2.Text!.Count);

        Assert.Single(abstractionLimitsSection2.SubResults);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
            
        Assert.NotNull(section2Sub1.SubResults);            
        Assert.Equal(6, section2Sub1.SubResults.Count);
        
        abstractionPoint = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "PointCondition")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("Abstraction Point B", abstractionPoint);
        
        perHour = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("46", perHour);
        
        perHourUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("19.2", perSecond);
        
        perSecondUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection3 = abstractionLimitsSection.SubResults[2];
        Assert.Equal(3, abstractionLimitsSection3.Text!.Count);

        Assert.NotNull(abstractionLimitsSection3.SubResults);
        Assert.Single(abstractionLimitsSection3.SubResults);
        
        var section3Sub1 = abstractionLimitsSection3.SubResults[0];
        Assert.Equal(6, section3Sub1.SubResults.Count);
        
        perHour = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("69", perHour);
        
        perHourUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("19.2", perSecond);
        
        perSecondUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection4 = abstractionLimitsSection.SubResults[3];
        Assert.Equal(3, abstractionLimitsSection4.Text!.Count);

        Assert.NotNull(abstractionLimitsSection4.SubResults);
        Assert.Single(abstractionLimitsSection4.SubResults);

        var section4Sub1 = abstractionLimitsSection4.SubResults[0];
        Assert.Equal(6, section4Sub1.SubResults.Count);
        
        perHour = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("137", perHour);
        
        perHourUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("38.1", perSecond);
        
        perSecondUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection5 = abstractionLimitsSection.SubResults[4];
        Assert.Equal(3, abstractionLimitsSection5.Text!.Count);

        Assert.NotNull(abstractionLimitsSection5.SubResults);
        Assert.Single(abstractionLimitsSection5.SubResults);

        var section5Sub1 = abstractionLimitsSection5.SubResults[0];
        Assert.Equal(6, section5Sub1.SubResults.Count);
        
        perHour = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("69", perHour);
        
        perHourUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("19.2", perSecond);
        
        perSecondUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection6 = abstractionLimitsSection.SubResults[5];
        Assert.Equal(3, abstractionLimitsSection6.Text!.Count);

        Assert.NotNull(abstractionLimitsSection6.SubResults);
        Assert.Single(abstractionLimitsSection6.SubResults);

        var section6Sub1 = abstractionLimitsSection6.SubResults[0];
        Assert.Equal(6, section6Sub1.SubResults.Count);
        
        perHour = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("91", perHour);
        
        perHourUnits = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("25.3", perSecond);
        
        perSecondUnits = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection7 = abstractionLimitsSection.SubResults[6];
        Assert.Equal(5, abstractionLimitsSection7.Text!.Count);

        Assert.NotNull(abstractionLimitsSection7.SubResults);
        Assert.Single(abstractionLimitsSection7.SubResults);

        var section7Sub1 = abstractionLimitsSection7.SubResults[0];
        Assert.Equal(6, section7Sub1.SubResults.Count);
        
        var perDay = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("1440", perDay);
        
        var perDayUnits = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        var perYear = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("22862", perYear);
        
        var perYearUnits = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);                                
        
        var abstractionLimitsSection8 = abstractionLimitsSection.SubResults[7];
        Assert.Equal(7, abstractionLimitsSection8.Text!.Count);

        Assert.NotNull(abstractionLimitsSection8.SubResults);
        Assert.Single(abstractionLimitsSection8.SubResults);

        var section8Sub1 = abstractionLimitsSection8.SubResults[0];
        Assert.Equal(8, section8Sub1.SubResults.Count);
        
        perHour = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("251", perHour);
        
        perHourUnits = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("4091", perDay);
        
        perDayUnits = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        perYear = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("190000", perYear);
        
        perYearUnits = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var linkedLicenceNumber = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("6/33/56/*G/0274/R02", linkedLicenceNumber);
        
        var abstractionLimitsSection9 = abstractionLimitsSection.SubResults[8];
        Assert.Equal(5, abstractionLimitsSection9.Text!.Count);

        Assert.NotNull(abstractionLimitsSection9.SubResults);
        Assert.Single(abstractionLimitsSection9.SubResults);

        var section9Sub1 = abstractionLimitsSection9.SubResults[0];
        Assert.Equal(9, section9Sub1.SubResults.Count);
        
        perHour = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("46", perHour);
        
        perHourUnits = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("1091", perDay);
        
        perDayUnits = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        perYear = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("40900", perYear);
        
        perYearUnits = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        linkedLicenceNumber = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("6/33/56/*G/0274/R02", linkedLicenceNumber);
        
        var abstractionLimitsSection10 = abstractionLimitsSection.SubResults[9];
        Assert.Equal(9, abstractionLimitsSection10.Text!.Count);

        Assert.NotNull(abstractionLimitsSection10.SubResults);
        Assert.Single(abstractionLimitsSection10.SubResults);

        var section10Sub1 = abstractionLimitsSection10.SubResults[0];
        Assert.Equal(10, section10Sub1.SubResults.Count);
        
        perHour = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("205", perHour);
        
        perHourUnits = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("3000", perDay);
        
        perDayUnits = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        perYear = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("190000", perYear);
        
        perYearUnits = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);                
        
        linkedLicenceNumber = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("6/33/56/*G/0274/R02", linkedLicenceNumber);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);        
        Assert.Equal("AN/033/0051/004", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);

        var allText = string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!);
        Assert.Equal("4. PURPOSES OF ABSTRACTION 4.1 Trickle irrigation. 4.2 Filling a reservoir for subsequent trickle irrigation.  Licence Serial No: AN/033/0051/004", allText); // TODO licence serial number bit shouldnt be here

        Assert.Equal(["PURPOSES OF ABSTRACTION"], purposeResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.BetweenLabels, purposeResult.MatchedPosition);
        
        Assert.Single(purposeResult.SubResults);
        var purposePointGroup = purposeResult.SubResults.Single();
        Assert.Equal("PurposePointGroup", purposePointGroup.MatchedLabel!.Name);

        var purposePointGroupSubResults = purposePointGroup.SubResults;
        Assert.Equal(2, purposePointGroupSubResults.Count);

        var purpose1 = purposePointGroupSubResults[0];
        Assert.Equal("4.1 Trickle irrigation.",
            string.Join(' ', purpose1.Text?.Select(x => x.Text).ToArray()!));

        var purpose2 = purposePointGroupSubResults[1];
        Assert.Equal("4.2 Filling a reservoir for subsequent trickle irrigation.  Licence Serial No: AN/033/0051/004",
            string.Join(' ', purpose2.Text?.Select(x => x.Text).ToArray()!)); // TODO licence serial number bit shouldnt be here
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService, NaldDataLookupService)).Last();
        
        var primaryLicence = agreedSchemaLicenceGroup.Licences[0];
        
        Assert.Single(primaryLicence.LinkedLicences);

        Assert.Equal("6/33/56/*G/0274/R02", primaryLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(primaryLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        
        Assert.Equal(7, primaryLicence.Points.Length);
        Assert.Equal("TL 75736 94136 Abstraction point A Map 1", primaryLicence.Points[0].Description);
        Assert.NotNull(primaryLicence.Points[0].ContainedIn);
        Assert.Single(primaryLicence.Points[0].ContainedIn!);
        Assert.Equal("Points", primaryLicence.Points[0].ContainedIn![0].SectionName);
        Assert.Equal(InformationSource.Document, primaryLicence.Points[0].ContainedIn![0].Source);
    }

    [Fact]
    public async Task WalderseyFarmsLimited_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application – Renewal – Licence Issued – 24062022.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(17, records.Text!.Count);
        
        var additionalInformation = resultList.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(54, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var periodsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "PeriodsOfAbstraction");
        
        Assert.NotNull(periodsResult);
        var allPeriodsText = string.Join(' ', periodsResult.Text?.Select(x => x.Text).ToArray()!);
        Assert.Equal("5.1 For Purposes 4.1 and 4.2 From 1 November to 31 March inclusive. " +
            "5.2 For Purpose 4.3 From 1 April to 31 October inclusive.", allPeriodsText);

        Assert.NotNull(periodsResult.SubResults);
        Assert.Equal(2, periodsResult.SubResults.Count);

        var periodSubSection1 = periodsResult.SubResults[0];
        Assert.Equal(3, periodSubSection1.SubResults.Count);
        
        Assert.Equal("5.1 For Purposes 4.1 and 4.2" , periodSubSection1.Text![0].Text);
        Assert.Equal("From 1 November to 31 March inclusive.", periodSubSection1.Text![1].Text);

        var periodSubSection1PurposesText = periodSubSection1.SubResults[1].Text!.Single().Text;
        Assert.Equal("4.1 and 4.2", periodSubSection1PurposesText);
        
        Assert.Equal(2, periodSubSection1.SubResults[1].SubResults.Count);
        Assert.Equal("4.1", periodSubSection1.SubResults[1].SubResults[0].Text!.Single().Text);
        Assert.Equal("4.2", periodSubSection1.SubResults[1].SubResults[1].Text!.Single().Text);

        var periodSubSection1Text = periodSubSection1.SubResults[2].Text!.Single().Text;
        Assert.Equal("From 1 November to 31 March inclusive", periodSubSection1Text);
        
        Assert.Equal(2, periodSubSection1.SubResults[1].SubResults.Count);
        
        var periodSubSection2 = periodsResult.SubResults[1];
        Assert.Equal(3, periodSubSection2.SubResults.Count);
        
        Assert.Equal("5.2 For Purpose 4.3", periodSubSection2.Text![0].Text);
        Assert.Equal("From 1 April to 31 October inclusive.", periodSubSection2.Text![1].Text);        
        
        var periodSubSection2PurposesText = periodSubSection2.SubResults[1].Text!.Single().Text;
        Assert.Equal("4.3", periodSubSection2PurposesText);
        
        Assert.Single(periodSubSection2.SubResults[1].SubResults);
        Assert.Equal("4.3", periodSubSection2.SubResults[1].SubResults[0].Text!.Single().Text);
        
        var periodSubSection2Text = periodSubSection2.SubResults[2].Text!.Single().Text;
        Assert.Equal("From 1 April to 31 October inclusive", periodSubSection2Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Waldersey Farms Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("6/33/47/*S/0172/R01", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(28, abstractionLimitsSection.Text!.Count);
        Assert.Equal(5, abstractionLimitsSection.SubResults.Count);
        Assert.Equal(4, abstractionLimitsSection.SubResults[0].Text!.Count);
        
        Assert.NotNull(abstractionLimitsSection.SubResults);
        Assert.Equal(5, abstractionLimitsSection.SubResults.Count);
        Assert.Equal(30, abstractionLimitsSection.LabelStartLineNumber);
        
        var section1Point1 = abstractionLimitsSection.SubResults[0];
        Assert.Equal(4, section1Point1.Text!.Count);
        Assert.NotNull(section1Point1.SubResults);
        Assert.Single(section1Point1.SubResults);
        
        var point1Sub1 = section1Point1.SubResults[0];
        Assert.Equal(7, point1Sub1.SubResults.Count);

        var perDay = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(33, perDay.LabelStartLineNumber);
        Assert.Equal("2000", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perHour = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("83", perHour);
        
        var perHourUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var perSecond = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per second")) == true);

        Assert.NotNull(perSecond);
        Assert.Equal(34, perSecond.LabelStartLineNumber);
        Assert.Equal("23.1", perSecond.Text?.FirstOrDefault()?.Text);
            
        var perSecondUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(3, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(4, section2Sub1.SubResults.Count);
        
        var perYear = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per year")) == true);

        Assert.NotNull(perYear);
        Assert.Equal(37, perYear.LabelStartLineNumber);
        Assert.Equal("68000", perYear.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var abstractionLimitsSection3 = abstractionLimitsSection.SubResults[2];
        Assert.Equal(2, abstractionLimitsSection3.Text!.Count);

        Assert.NotNull(abstractionLimitsSection3.SubResults);
        Assert.Single(abstractionLimitsSection3.SubResults);

        var section3Sub1 = abstractionLimitsSection3.SubResults[0];
        Assert.Equal(3, section3Sub1.SubResults.Count);
        
        perYear = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per year")) == true);

        Assert.NotNull(perYear);
        Assert.Equal(2, perYear.LabelStartLineNumber);
        Assert.Equal("68000", perYear.Text?.FirstOrDefault()?.Text);
        
        perYearUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var abstractionLimitsSection4 = abstractionLimitsSection.SubResults[3];
        Assert.Equal(5, abstractionLimitsSection4.Text!.Count);

        Assert.NotNull(abstractionLimitsSection4.SubResults);
        Assert.Single(abstractionLimitsSection4.SubResults);

        var section4Sub1 = abstractionLimitsSection4.SubResults[0];
        Assert.Equal(9, section4Sub1.SubResults.Count);

        perHour = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("219", perHour);
        
        perHourUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var perYearList = section4Sub1.SubResults
            .Where(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per year")) == true)
            .ToList();

        perYear = perYearList.FirstOrDefault();
        
        Assert.NotNull(perYear);
        Assert.Equal(6, perYear.LabelStartLineNumber);
        Assert.Equal("61200", perYear.Text?.FirstOrDefault()?.Text);
        

        Assert.Equal(1, section4Sub1.SubResults
            .Count(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year"))));
        
        perYearUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);   
        
        perDay = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(5, perDay.LabelStartLineNumber);
        Assert.Equal("5256", perDay.Text?.FirstOrDefault()?.Text);

        perDayUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var abstractionLimitsSection5 = abstractionLimitsSection.SubResults[4];
        Assert.Equal(11, abstractionLimitsSection5.Text!.Count);

        Assert.NotNull(abstractionLimitsSection5.SubResults);
        Assert.Single(abstractionLimitsSection5.SubResults);

        var section5Sub1 = abstractionLimitsSection5.SubResults[0];
        Assert.Equal(11, section5Sub1.SubResults.Count);

        perYearList = section5Sub1.SubResults
            .Where(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per year")) == true)
            .ToList();

        perYear = perYearList.FirstOrDefault();
        
        Assert.NotNull(perYear);
        Assert.Equal(14, perYear.LabelStartLineNumber);
        Assert.Equal("68000", perYear.Text?.FirstOrDefault()?.Text);
        
        perYearUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per year")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        perHour = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("219", perHour);
        
        perHourUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per hour")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);                        
        
        perDay = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(13, perDay.LabelStartLineNumber);
        Assert.Equal("5256", perDay.Text?.FirstOrDefault()?.Text);

        perDayUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var linkedLicenceNumber = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("AN/033/0047/018", linkedLicenceNumber);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");  

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        
        Assert.Equal("DocumentPurposesAll", purposeResult.MatchedLabel!.Name);
        Assert.Equal(["PURPOSES OF ABSTRACTION"], purposeResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.BetweenLabels, purposeResult.MatchedPosition);
        
        var allPurposeText = string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!);
        
        Assert.Equal("4. PURPOSES OF ABSTRACTION 4.1 From Point 2.1 Transfer for subsequent discharge and re-abstraction for spray irrigation from" 
                     + " the points specified in condition 2.2 of this licence and points specified in"
                     + " condition 2.1 of licence AN/033/0047/018."
                     + " 4.2 Filling a reservoir for subsequent spray irrigation."
                     + " 4.3 From Point 2.2"
                     + " Spray Irrigation.",
            allPurposeText);
        
        Assert.Equal(2, purposeResult.SubResults.Count);
        
        var purposePointGroup1 = purposeResult.SubResults[0];
        Assert.Equal("PurposePointGroup", purposePointGroup1.MatchedLabel!.Name);
        
        var purposePointGroup1AllText = string.Join(' ', purposePointGroup1.Text?.Select(x => x.Text).ToArray()!);
        Assert.Equal("4.1 From Point 2.1 Transfer for subsequent discharge and re-abstraction for spray irrigation from"
                     + " the points specified in condition 2.2 of this licence and points specified in"
                     + " condition 2.1 of licence AN/033/0047/018. 4.2 Filling a reservoir for subsequent spray irrigation.",
            purposePointGroup1AllText);
        
        Assert.Equal(3, purposePointGroup1.SubResults.Count);

        var purposeGroup1PointGroupName = purposePointGroup1.SubResults[0];
        Assert.Equal("PointGroupName", purposeGroup1PointGroupName.MatchedLabel!.Name);
        Assert.Equal("2.1", purposeGroup1PointGroupName.Text?.FirstOrDefault()?.Text);
        
        var purpose1 = purposePointGroup1.SubResults[1];
        Assert.Equal("Purposes", purpose1.MatchedLabel!.Name);
        Assert.Equal(4, purpose1.Text!.Count);
        
        var purpose1AllText = string.Join(' ', purpose1.Text?.Select(x => x.Text).ToArray()!);
        
        Assert.Equal("4.1 From Point 2.1 Transfer for subsequent discharge and re-abstraction for spray irrigation from"
                     + " the points specified in condition 2.2 of this licence and points specified in"
                     + " condition 2.1 of licence AN/033/0047/018.",
            purpose1AllText);

        Assert.NotNull(purpose1.SubResults);
        Assert.Equal(3, purpose1.SubResults.Count);

        var purpose1PurposeNumber = purpose1.SubResults[0];
        Assert.Equal("PurposeNumber", purpose1PurposeNumber.MatchedLabel?.Name);
        Assert.Equal("4.1", purpose1PurposeNumber.Text!.Single().Text);

        var purpose1TextWithoutPoints = purpose1.SubResults[1];
        Assert.Equal("TextWithoutPoints", purpose1TextWithoutPoints.MatchedLabel!.Name);
        
        var purpose1TextOnly = string.Join(' ', purpose1TextWithoutPoints.Text?.Select(x => x.Text).ToArray()!);
        Assert.Equal("Transfer for subsequent discharge and re-abstraction for spray irrigation from" 
            + " the points specified in condition 2.2 of this licence and points specified in"
            + " condition 2.1 of licence AN/033/0047/018", purpose1TextOnly);
        
        var purpose2 = purposePointGroup1.SubResults[2];
        Assert.Equal("Purposes", purpose2.MatchedLabel!.Name);
        Assert.Single(purpose2.Text!);
        
        var purpose2AllText = purpose2.Text?.Select(x => x.Text).ToArray()!;
        
        Assert.Equal("4.2 Filling a reservoir for subsequent spray irrigation.",
            string.Join(' ', purpose2AllText));

        Assert.NotNull(purpose2.SubResults);
        Assert.Equal(2, purpose2.SubResults.Count);
        
        var purpose2PurposeNumber = purpose2.SubResults[0];
        Assert.Equal("PurposeNumber", purpose2PurposeNumber.MatchedLabel?.Name);
        Assert.Equal("4.2", purpose2PurposeNumber.Text!.Single().Text);
        
        var purpose2TextWithoutPoints = purpose2.SubResults[1];
        Assert.Equal("TextWithoutPoints", purpose2TextWithoutPoints.MatchedLabel!.Name);
        Assert.Equal("Filling a reservoir for subsequent spray irrigation",
            string.Join(' ', purpose2TextWithoutPoints.Text?.Select(x => x.Text).ToArray()!));
        
        var purposePointGroup2 = purposeResult.SubResults[1];
        
        var purposePointGroup2AllText = string.Join(' ', purposePointGroup2.Text?.Select(x => x.Text).ToArray()!);
        Assert.Equal("4.3 From Point 2.2 Spray Irrigation.",
            purposePointGroup2AllText);
        
        var purposeGroup2PointGroupName = purposePointGroup2.SubResults[0];
        Assert.Equal("PointGroupName", purposeGroup2PointGroupName.MatchedLabel!.Name);
        Assert.Equal("2.2", purposeGroup2PointGroupName.Text![0].Text);
        
        var purpose3 = purposePointGroup2.SubResults[1];
        Assert.Equal("Purposes", purpose3.MatchedLabel!.Name);
       
        Assert.Equal("4.3 From Point 2.2 Spray Irrigation.", string.Join(' ', purpose3.Text?.Select(x => x.Text).ToArray()!));
        
        Assert.NotNull(purpose3.SubResults);
        Assert.Equal(2, purpose3.SubResults.Count);
        
        var purpose3PurposeNumber = purpose3.SubResults[0];
        Assert.Equal("PurposeNumber", purpose3PurposeNumber.MatchedLabel?.Name);
        Assert.Equal("4.3", purpose3PurposeNumber.Text!.Single().Text);
        
        var purpose3TextWithoutPoints = purpose3.SubResults[1];
        Assert.Equal("TextWithoutPoints", purpose3TextWithoutPoints.MatchedLabel?.Name);
        var purpose3TextOnly = string.Join(' ', purpose3TextWithoutPoints.Text?.Select(x => x.Text).ToArray()!);
        Assert.Equal("Spray Irrigation", purpose3TextOnly);
        
        var pointsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");

        Assert.NotNull(pointsResult);
        Assert.False(pointsResult.IsOcr);
        Assert.Equal("DocumentPointsAll", pointsResult.MatchedLabel!.Name);
        
        Assert.Equal(52, pointsResult.Text!.Count);
        Assert.Equal("2.1 For Purpose 4.1 and 4.2", pointsResult.Text![0].Text);
        Assert.Equal("Between National Grid References TL 55782 94571 and TL 55844 94741", pointsResult.Text![1].Text);
        Assert.Equal("marked 'Point A' and 'Point B' on Map 1.", pointsResult.Text![2].Text);
        Assert.Equal("2.2 For Purpose 4.3", pointsResult.Text![3].Text);
        Assert.Equal("National Grid References", pointsResult.Text![4].Text);
        Assert.Equal("From To", pointsResult.Text![5].Text);
        Assert.Equal("TL5584494741 TL5453692523", pointsResult.Text![6].Text);
        Assert.Equal("TL5502493346 TL5522093137", pointsResult.Text![7].Text);
        
        Assert.Equal(2, pointsResult.SubResults.Count);

        var pointPurposeGroup1 = pointsResult.SubResults[0];
        Assert.Equal("PointPurposeGroup", pointPurposeGroup1.MatchedLabel!.Name);
        Assert.Equal(3, pointPurposeGroup1.Text!.Count);
        
        var pointPurposeGroup1Name = pointPurposeGroup1.SubResults[0];
        Assert.Equal("PurposeGroupName", pointPurposeGroup1Name.MatchedLabel!.Name);
        Assert.Equal("4.1 and 4.2", pointPurposeGroup1Name.Text!.Single().Text);
        
        Assert.Equal(2, pointPurposeGroup1Name.SubResults.Count);
        Assert.Equal("4.1", pointPurposeGroup1Name.SubResults[0].Text?.FirstOrDefault()?.Text);
        Assert.Equal("4.2", pointPurposeGroup1Name.SubResults[1].Text?.FirstOrDefault()?.Text);
        
        var point1 = pointPurposeGroup1.SubResults[1];
        Assert.Equal("Point", point1.MatchedLabel!.Name);
        
        Assert.Equal("2.1 For Purpose 4.1 and 4.2 Between National Grid References TL 55782 94571 and TL 55844 94741" 
                + " marked 'Point A' and 'Point B' on Map 1.",
            string.Join(' ', point1.Text?.Select(x => x.Text).ToArray()!));
        
        Assert.NotNull(point1.SubResults);
        Assert.Equal(3, point1.SubResults.Count);

        var point1PointNumber = point1.SubResults[0];
        Assert.Equal("PointPointNumber", point1PointNumber.MatchedLabel!.Name);
        Assert.Equal("2.1", point1PointNumber.Text![0].Text);
        
        var point1PurposeLink = point1.SubResults[1];
        Assert.Equal("PurposeLink", point1PurposeLink.MatchedLabel!.Name);
        Assert.Equal("4.1 and 4.2", point1PurposeLink.Text![0].Text);

        Assert.NotNull(point1PurposeLink.SubResults);
        Assert.Equal(2, point1PurposeLink.SubResults.Count);

        var point1PurposeLinkSub1 = point1PurposeLink.SubResults[0];
        Assert.Equal("4.1", point1PurposeLinkSub1.Text![0].Text);
        
        var point1PurposeLinkSub2 = point1PurposeLink.SubResults[1];        
        Assert.Equal("4.2", point1PurposeLinkSub2.Text![0].Text);
        
        var point1TTextWithoutPurposeAndPoint= point1.SubResults[2];
        Assert.Equal("PointTextWithoutPurposeAndPoint", point1TTextWithoutPurposeAndPoint.MatchedLabel!.Name);
        Assert.Equal("Between National Grid References TL 55782 94571 and TL 55844 94741 marked 'Point A' and 'Point B' on Map 1.",
            string.Join(' ', point1TTextWithoutPurposeAndPoint.Text?.Select(x => x.Text).ToArray()!));
        
        var pointPurposeGroup2 = pointsResult.SubResults[1];
        Assert.Equal("PointPurposeGroup", pointPurposeGroup2.MatchedLabel!.Name);
        Assert.Equal(49, pointPurposeGroup2.Text!.Count);
        
        var pointPurposeGroup2Text = pointPurposeGroup2.Text!;

        Assert.Equal(49, pointPurposeGroup2Text.Count);
        Assert.Equal("2.2 For Purpose 4.3", pointPurposeGroup2Text[0].Text);
        Assert.Equal("National Grid References", pointPurposeGroup2Text[1].Text);
        Assert.Equal("From To", pointPurposeGroup2Text[2].Text);
        Assert.Equal("TL5584494741 TL5453692523", pointPurposeGroup2Text[3].Text);
        //...
        Assert.Equal("TL5616889665 TL5658389810", pointPurposeGroup2Text[48].Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService, NaldDataLookupService)).Last();

        Assert.NotNull(agreedSchemaLicenceGroup.Licences);
        Assert.Equal(2, agreedSchemaLicenceGroup.Licences.Length);
        
        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Equal(filename, primaryLicence.Filename);
        Assert.Equal("6/33/47/*S/0172/R01", primaryLicence.LicenceNumber?.Value);

        var points = primaryLicence.Points;
        Assert.Equal(47, points.Length);
        
        var primaryPoint1 = points[0];
        Assert.Equal("2.1", primaryPoint1.Id);
        Assert.Equal("Between National Grid References TL 55782 94571 and TL 55844 94741 marked 'Point A' and 'Point B' on Map 1", primaryPoint1.Description);
        Assert.Equal(2, primaryPoint1.PurposeIds!.Length);
        Assert.Equal("4.1", primaryPoint1.PurposeIds[0]);
        Assert.Equal("4.2", primaryPoint1.PurposeIds[1]);
        
        var primaryPoint2 = points[1];
        Assert.Equal("2.2 TL5584494741 to TL5453692523", primaryPoint2.Id);
        Assert.Equal(33, primaryPoint2.Description!.Length);
        Assert.StartsWith("From TL5584494741 to TL5453692523", primaryPoint2.Description);
        Assert.Single(primaryPoint2.PurposeIds!);
        Assert.Equal("4.3", primaryPoint2.PurposeIds![0]);
        
        var primaryPoint47 = points[46];
        Assert.Equal("2.2 TL5616889665 to TL5658389810", primaryPoint47.Id);
        Assert.Equal(33, primaryPoint47.Description!.Length);
        Assert.StartsWith("From TL5616889665 to TL5658389810", primaryPoint47.Description);
        Assert.Single(primaryPoint47.PurposeIds!);
        Assert.Equal("4.3", primaryPoint47.PurposeIds![0]);

        var purposes = primaryLicence.Purposes;
        Assert.Equal(3, purposes.Length);
        
        var primaryPurpose1 = purposes[0];
        Assert.Equal("4.1", primaryPurpose1.Id);
        Assert.StartsWith("Transfer for subsequent discharge and", primaryPurpose1.Description);
        Assert.Single(primaryPurpose1.PointIds!);
        Assert.Equal("2.1", primaryPurpose1.PointIds![0]);
        
        var primaryPurpose2 = purposes[1];
        Assert.Equal("4.2", primaryPurpose2.Id);
        Assert.StartsWith("Filling a reservoir for subsequent", primaryPurpose2.Description);
        Assert.Single(primaryPurpose2.PointIds!);
        Assert.Equal("2.1", primaryPurpose2.PointIds![0]);
        
        var primaryPurpose3 = purposes[2];
        Assert.Equal("4.3", primaryPurpose3.Id);
        Assert.Equal("Spray Irrigation", primaryPurpose3.Description);
        Assert.Single(primaryPurpose3.PointIds!);
        Assert.Equal("2.2", primaryPurpose3.PointIds![0]);
        
        Assert.Single(primaryLicence.LinkedLicences);

        Assert.Equal("AN/033/0047/018", primaryLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, primaryLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("Purposes", primaryLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("DischargeAndReabstractionCondition", primaryLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
    }
    
    [Fact]
    public async Task LicenceToEA_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application Renewal Issued Licence- 25.01.2024.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(11, records.Text!.Count);
        
        var additionalInformation = resultList.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(52, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Environment Agency", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/0390/R01", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(60, abstractionLimitsSection.Text!.Count);
        Assert.Equal(8, abstractionLimitsSection.SubResults.Count);
        Assert.Equal(3, abstractionLimitsSection.SubResults[0].Text!.Count);        
        
        var point1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(point1.SubResults);
        Assert.Equal(3, point1.Text!.Count);
        
        var point1Sub1 = point1.SubResults[0];
        Assert.NotNull(point1Sub1.SubResults);
        Assert.Equal(5, point1Sub1.SubResults.Count);

        var perDay = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(18, perDay.LabelStartLineNumber);
        Assert.Equal("2500", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perSecond = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("29", perSecond);
        
        var perSecondUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(3, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(5, section2Sub1.SubResults.Count);
        
        perDay = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(21, perDay.LabelStartLineNumber);
        Assert.Equal("5000", perDay.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        perSecond = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("57.9", perSecond);
        
        perSecondUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection3 = abstractionLimitsSection.SubResults[2];
        Assert.Equal(3, abstractionLimitsSection3.Text!.Count);

        Assert.NotNull(abstractionLimitsSection3.SubResults);
        Assert.Single(abstractionLimitsSection3.SubResults);

        var section3Sub1 = abstractionLimitsSection3.SubResults[0];
        Assert.Equal(5, section3Sub1.SubResults.Count);

        perDay = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(24, perDay.LabelStartLineNumber);
        Assert.Equal("5000", perDay.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        perSecond = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("57.9", perSecond);
        
        perSecondUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection4 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(3, abstractionLimitsSection4.Text!.Count);

        Assert.NotNull(abstractionLimitsSection4.SubResults);
        Assert.Single(abstractionLimitsSection4.SubResults);

        var section4Sub1 = abstractionLimitsSection4.SubResults[0];
        Assert.Equal(5, section4Sub1.SubResults.Count);
        
        perDay = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text?.Any(text => text.Text.Contains("per day")) == true);

        Assert.NotNull(perDay);
        Assert.Equal(21, perDay.LabelStartLineNumber);
        Assert.Equal("5000", perDay.Text?.FirstOrDefault()?.Text); // TODO there are 2 5000s and 1 5300
        
        perDayUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per day")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        perSecond = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("57.9", perSecond); // TODO there is also 61.3
        
        perSecondUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text?.Any(text => text.Text.Contains("per second")) == true)?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        // TODO 4 more sections
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService, NaldDataLookupService)).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(primaryLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenNearNextLineIsCompany_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application - Minor Variation  Issued licence -007-13122023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(13, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(25, additionalInformation.Text!.Count);
        
        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);          
        
        Assert.NotNull(resultList.FirstOrDefault(result => result.LabelGroupName == "MeansOfAbstraction"));
        Assert.NotNull(resultList.FirstOrDefault(result => result.LabelGroupName == "PeriodsOfAbstraction"));
        Assert.NotNull(resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes"));
        Assert.NotNull(resultList.FirstOrDefault(result => result.LabelGroupName == "Points"));
        Assert.NotNull(resultList.FirstOrDefault(result => result.LabelGroupName == "DateOfIssue"));       
        Assert.NotNull(resultList.FirstOrDefault(result => result.LabelGroupName == "DateOfOriginalIssue"));
        Assert.NotNull(resultList.FirstOrDefault(result => result.LabelGroupName == "DateEffective"));
        Assert.NotNull(resultList.FirstOrDefault(result => result.LabelGroupName == "DateOfExpiry"));
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");

        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Armstrongs Aggregates Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("NW/071/0309/007", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsSection = resultList.Single(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);

        Assert.Equal(28, abstractionLimitsSection.Text!.Count);
        
        Assert.Equal(4, abstractionLimitsSection.SubResults.Count);
        var sectionPoint1 = abstractionLimitsSection.SubResults[0];

        Assert.Single(sectionPoint1.SubResults);

        var sectionPoint1Sub1 = sectionPoint1.SubResults[0];
        Assert.Equal(9, sectionPoint1Sub1.SubResults.Count);
        Assert.Single(sectionPoint1Sub1.SubResults[0].Text!);
        
        var sectionPoint2 = abstractionLimitsSection.SubResults[1];

        Assert.Single(sectionPoint2.SubResults);

        var sectionPoint2Sub1 = sectionPoint2.SubResults[0];
        Assert.Equal(9, sectionPoint2Sub1.SubResults.Count);
        Assert.Single(sectionPoint2Sub1.SubResults[0].Text!);
        
        var sectionPoint3 = abstractionLimitsSection.SubResults[2];

        Assert.Single(sectionPoint3.SubResults);

        var sectionPoint3Sub1 = sectionPoint3.SubResults[0];
        Assert.Equal(9, sectionPoint3Sub1.SubResults.Count);
        Assert.Single(sectionPoint3Sub1.SubResults[0].Text!);
        
        var sectionPoint4 = abstractionLimitsSection.SubResults[3];

        Assert.Single(sectionPoint4.SubResults);

        var sectionPoint4Sub1 = sectionPoint4.SubResults[0];
        Assert.Equal(9, sectionPoint4Sub1.SubResults.Count);
        Assert.Single(sectionPoint4Sub1.SubResults[0].Text!);
        
        // TODO expand this section + add others
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService, NaldDataLookupService)).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(primaryLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task XXXWhenSameLineIsCompany1Line_AndAbstractionLimitsToBeFoundWithSpellingMistake_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893476.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        //Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal("4. PURPOSE OF ABSTRACTION", purposeResult.Text?[0].Text);
        Assert.Equal("4.1 Fish farm and fishery.", purposeResult.Text?[1].Text);
        Assert.Equal(["PURPOSE OF ABSTRACTION"], purposeResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.BetweenLabels, purposeResult.MatchedPosition);
        Assert.Equal("4.1", purposeResult.SubResults[0].SubResults[0].SubResults[0].Text!.First().Text);
        Assert.Equal("Fish farm and fishery", purposeResult.SubResults[0].SubResults[0].SubResults[1].Text!.First().Text);
        
        var pointsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");

        Assert.NotNull(pointsResult);
        Assert.False(pointsResult.IsOcr);
        Assert.Equal("DocumentPointsAll", pointsResult.MatchedLabel!.Name);
        
        Assert.Single(pointsResult.Text!);
        Assert.Equal("2.1 At National Grid Reference SJ 5179 4988 marked \"C\" on the map.", pointsResult.Text![0].Text);
        
        var pointPurposeGroup1 = pointsResult.SubResults[0];
        Assert.Equal("PointPurposeGroup", pointPurposeGroup1.MatchedLabel!.Name);

        // NOTE - No PurposeGroupName
        
        var point = pointPurposeGroup1.SubResults[0];
        Assert.Equal("Point", point.MatchedLabel!.Name);
        
        Assert.Equal("2.1 At National Grid Reference SJ 5179 4988 marked \"C\" on the map.", point.Text!.First().Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("J & S Accessories Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimits = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimits);
        Assert.False(abstractionLimits.IsOcr);
        Assert.Equal(10, abstractionLimits.Text!.Count);
        Assert.Equal("The aggregate quality of water authorised to be abstracted under this licence", abstractionLimits.Text![4].Text);
        Assert.Single(abstractionLimits.SubResults);

        var abstractionLimitsPoint = abstractionLimits.SubResults[0];
        Assert.Equal(2, abstractionLimitsPoint.SubResults.Count); // TODO should investigate this later if this should be 2 or 3
        
        var abstractionLimitPointSub1 = abstractionLimitsPoint.SubResults[0];
        
        Assert.Equal("20", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("475", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
            && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("173453", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
            && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);

        var abstractionLimitPointSub2 = abstractionLimitsPoint.SubResults[1];
        
        var linkedLicenceNumbers = abstractionLimitPointSub2.SubResults
            .Where(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")
            .ToList();

        Assert.Equal(2, linkedLicenceNumbers.Count);
        Assert.Single(linkedLicenceNumbers[0].Text!);
        Assert.Single(linkedLicenceNumbers[1].Text!);

        var linkedLicenceNumber1 = linkedLicenceNumbers[0].Text![0].Text;
        Assert.Equal("25 68 001 247", linkedLicenceNumber1);

        var linkedLicenceNumber2 = linkedLicenceNumbers[1].Text![0].Text;
        Assert.Equal("25 68 001 248", linkedLicenceNumber2);
        
        var linkedLicences = abstractionLimitPointSub2.SubResults
            .Where(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicence")
            .ToList();
        
        Assert.Empty(linkedLicences);
        
        // TODO and the other licence
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("25 68 001 249", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService,
            NaldDataLookupService,
            new DmsFileData
            {
                FileId = Guid.Parse("10000000-0000-0000-0000-000000000000"),
                DmsPath = "main path"
            })).Last();

        Assert.NotNull(agreedSchemaLicenceGroup.Licences);
        Assert.Equal(3, agreedSchemaLicenceGroup.Licences.Length);
        
        Assert.Equal("25/68/001/249", agreedSchemaLicenceGroup.Licences[0].LicenceNumber?.Value);
        Assert.Equal("main path", agreedSchemaLicenceGroup.Licences[0].DmsPath);
        Assert.Equal("10000000-0000-0000-0000-000000000000", agreedSchemaLicenceGroup.Licences[0].DmsFileId.ToString());
        Assert.Equal(ScrapeStatus.Ok, agreedSchemaLicenceGroup.Licences[0].Status);
        Assert.Equal(LicenceType.SurfaceWaterAbstraction, agreedSchemaLicenceGroup.Licences[0].LicenceType);
        Assert.Equal(NaldLicenceStatus.Live, agreedSchemaLicenceGroup.Licences[0].NaldStatus);
        
        Assert.Equal("25/68/001/247", agreedSchemaLicenceGroup.Licences[1].LicenceNumber?.Value);
        Assert.Equal("Something to look for", agreedSchemaLicenceGroup.Licences[1].DmsPath);
        Assert.Equal("25/68/001/248", agreedSchemaLicenceGroup.Licences[2].LicenceNumber?.Value);
        Assert.Equal("Something to look for", agreedSchemaLicenceGroup.Licences[2].DmsPath);
        
        Assert.Equal("2568001247-LV20190619-2568001248-LV20190619-2568001249-LV20190619",
            agreedSchemaLicenceGroup.LicenceSetId);
        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Equal(2, primaryLicence.LinkedLicences.Length);
        
        Assert.Equal("25/68/001/247", primaryLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(LicenceType.GroundWaterAbstraction, primaryLicence.LinkedLicences[0].LicenceType);
        Assert.Equal(NaldLicenceStatus.Live, primaryLicence.LinkedLicences[0].NaldStatus);
        
        Assert.Equal(2, primaryLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, primaryLicence.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal(InformationSource.Document, primaryLicence.LinkedLicences[0].ContainedIn![0].Source);
        Assert.Equal(InformationDirection.Incoming, primaryLicence.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("ShallNotExceed", primaryLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal(InformationSource.OtherDocument, primaryLicence.LinkedLicences[0].ContainedIn![1].Source);
        
        Assert.Equal("25/68/001/248", primaryLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, primaryLicence.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, primaryLicence.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("ShallNotExceed", primaryLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        Assert.Equal(InformationSource.Document, primaryLicence.LinkedLicences[1].ContainedIn![0].Source);
        Assert.Equal(InformationDirection.Incoming, primaryLicence.LinkedLicences[1].ContainedIn![1].Direction);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[1].ContainedIn![1].SectionName);
        Assert.Equal("ShallNotExceed", primaryLicence.LinkedLicences[1].ContainedIn![1].LinkReason);      
        Assert.Equal(InformationSource.OtherDocument, primaryLicence.LinkedLicences[1].ContainedIn![1].Source);
        
        Assert.Equal(filename, primaryLicence.Filename);
        Assert.Equal("25/68/001/249", primaryLicence.LicenceNumber?.Value);
        
        Assert.Equal(3, primaryLicence.AbstractionLimits.Individual![0].Limits.Count);
        var limitGroup = primaryLicence.AbstractionLimits.Individual[0];
        
        Assert.Equal(LimitPeriodType.PerHour, limitGroup.Limits[0].PeriodType);
        Assert.Equal("cubic metres", limitGroup.Limits[0].Units);
        Assert.Equal(20, limitGroup.Limits[0].Value);
        
        Assert.Equal(LimitPeriodType.PerDay, limitGroup.Limits[1].PeriodType);
        Assert.Equal("cubic metres", limitGroup.Limits[1].Units);
        Assert.Equal(475, limitGroup.Limits[1].Value);
        
        Assert.Equal(LimitPeriodType.PerYear, limitGroup.Limits[2].PeriodType);
        Assert.Equal("cubic metres", limitGroup.Limits[2].Units);
        Assert.Equal(173453, limitGroup.Limits[2].Value);

        Assert.Single(primaryLicence.AbstractionLimits.Aggregates!);
        Assert.NotNull(primaryLicence.AbstractionLimits.Aggregates!.Single());
        
        var aggregate = primaryLicence.AbstractionLimits.Aggregates!.Single();
        Assert.Equal("2568001249-LV20190619-LL-2568001247-2568001248", aggregate.Id);
        Assert.NotNull(aggregate.Limits);
        Assert.Equal(2, aggregate.Limits.Count);
        
        Assert.Equal(LimitPeriodType.PerDay, aggregate.Limits[0].PeriodType);
        Assert.Equal("cubic metres", aggregate.Limits[0].Units);
        Assert.Equal(475, aggregate.Limits[0].Value);
        
        Assert.Equal(LimitPeriodType.PerYear, aggregate.Limits[1].PeriodType);
        Assert.Equal("cubic metres", aggregate.Limits[1].Units);
        Assert.Equal(173453, aggregate.Limits[1].Value);        
        
        Assert.NotNull(primaryLicence.LicenceVersion);
        Assert.Equal("LV20190619", primaryLicence.LicenceVersion.LicenceVersionId);

        Assert.Single(primaryLicence.Points);
        Assert.Equal("At National Grid Reference SJ 5179 4988 marked \"C\" on the map",
            primaryLicence.Points.First().Description);

        Assert.Single(primaryLicence.Purposes);
        Assert.Equal("Fish farm and fishery", primaryLicence.Purposes.First().Description);
        
        Assert.Null(primaryLicence.LicenceVersion.ExpiryDate);
        Assert.Equal(new DateTime(2019, 06, 19), primaryLicence.LicenceVersion.EffectiveDate);
        Assert.Equal(new DateTime(1995, 05, 09), primaryLicence.LicenceVersion.OriginalIssueDate);
        Assert.Equal(new DateTime(2019, 06, 19), primaryLicence.LicenceVersion.IssueDate);
        
        var firstLinkedLicence = agreedSchemaLicenceGroup.Licences[1];
        Assert.Equal("25/68/001/247", firstLinkedLicence.LicenceNumber?.Value);
        Assert.Single(firstLinkedLicence.AbstractionLimits.Aggregates!);
        
        Assert.NotNull(firstLinkedLicence.NoneSchemaData["issuedTo"]);
        Assert.Equal("J & S Accessories Limited", (string?)firstLinkedLicence.NoneSchemaData["issuedTo"]);
        
        Assert.NotNull(firstLinkedLicence.LicenceNumber);
        Assert.Equal("25/68/001/247", firstLinkedLicence.LicenceNumber?.Value);
        Assert.Equal(2, firstLinkedLicence.LinkedLicences.Length);
        Assert.Equal("25/68/001/248", firstLinkedLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("25/68/001/249", firstLinkedLicence.LinkedLicences[1].LicenceNumber);
        
        var secondLinkedLicence = agreedSchemaLicenceGroup.Licences[2];
        Assert.Equal("25/68/001/248", secondLinkedLicence.LicenceNumber?.Value);
        Assert.Single(secondLinkedLicence.AbstractionLimits.Aggregates!);
        
        Assert.NotNull(secondLinkedLicence.NoneSchemaData["issuedTo"]);
        Assert.Equal("J & S Accessories Limited", (string?)secondLinkedLicence.NoneSchemaData["issuedTo"]);

        Assert.NotNull(secondLinkedLicence.LicenceNumber);
        Assert.Equal("25/68/001/248", secondLinkedLicence.LicenceNumber?.Value);
        Assert.Equal(2, secondLinkedLicence.LinkedLicences.Length);
        Assert.Equal("25/68/001/247", secondLinkedLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("25/68/001/249", secondLinkedLicence.LinkedLicences[1].LicenceNumber);
        
        Assert.NotNull(agreedSchemaLicenceGroup.AggregateSets);
        Assert.Single(agreedSchemaLicenceGroup.AggregateSets);

        Assert.NotNull(agreedSchemaLicenceGroup.AggregateSets[0].Aggregates);
        Assert.Equal(3, agreedSchemaLicenceGroup.AggregateSets[0].Aggregates.Length);

        // Need to update these for comparison
        agreedSchemaLicenceGroup.Licences[0].LicenceVersion.DmsFileIdStatusDateUtc = new DateTime(2001, 2, 3);
        agreedSchemaLicenceGroup.Licences[0].LinkedLicences[0].LicenceVersion.DmsFileIdStatusDateUtc = new DateTime(2001, 2, 3);
        agreedSchemaLicenceGroup.Licences[0].LinkedLicences[1].LicenceVersion.DmsFileIdStatusDateUtc = new DateTime(2001, 2, 3);
        
        agreedSchemaLicenceGroup.Licences[1].LicenceVersion.DmsFileIdStatusDateUtc = new DateTime(2001, 2, 3);
        agreedSchemaLicenceGroup.Licences[1].LinkedLicences[0].LicenceVersion.DmsFileIdStatusDateUtc = new DateTime(2001, 2, 3);
        agreedSchemaLicenceGroup.Licences[1].LinkedLicences[1].LicenceVersion.DmsFileIdStatusDateUtc = new DateTime(2001, 2, 3);
        
        agreedSchemaLicenceGroup.Licences[2].LicenceVersion.DmsFileIdStatusDateUtc = new DateTime(2001, 2, 3);
        agreedSchemaLicenceGroup.Licences[2].LinkedLicences[0].LicenceVersion.DmsFileIdStatusDateUtc = new DateTime(2001, 2, 3);
        agreedSchemaLicenceGroup.Licences[2].LinkedLicences[1].LicenceVersion.DmsFileIdStatusDateUtc = new DateTime(2001, 2, 3);

        var actualJson = JsonSerializer.Serialize(agreedSchemaLicenceGroup, JsonHelper.GetSerializerOptions());
        var expectedJson =
            await File.ReadAllTextAsync("Data/2568001247-LV20190619-2568001248-LV20190619-2568001249-LV20190619.json");

        Assert.Equal("25/68/001/249", agreedSchemaLicenceGroup.Licences[0].LicenceNumber?.Value);
        Assert.Equal("10000000-0000-0000-0000-000000000000", agreedSchemaLicenceGroup.Licences[0].DmsFileId.ToString());
        Assert.Equal("FirstSeen", agreedSchemaLicenceGroup.Licences[0].LicenceVersion.DmsFileIdStatus);
        Assert.NotNull(agreedSchemaLicenceGroup.Licences[0].LicenceVersion.DmsFileIdStatusDateUtc);
        
        Assert.Equal("25/68/001/247", agreedSchemaLicenceGroup.Licences[0].LinkedLicences[0].LicenceNumber);
        Assert.Equal("fc901013-3c0e-008d-117a-b48fa58d8feb", agreedSchemaLicenceGroup.Licences[0].LinkedLicences[0].DmsFileId?.ToString());
        Assert.Equal("FirstSeen", agreedSchemaLicenceGroup.Licences[0].LinkedLicences[0].LicenceVersion.DmsFileIdStatus);
        Assert.NotNull(agreedSchemaLicenceGroup.Licences[0].LinkedLicences[0].LicenceVersion.DmsFileIdStatusDateUtc);
        
        Assert.Equal(
            expectedJson.Replace(" ", string.Empty).Replace("\n", string.Empty),
            actualJson.Replace(" ", string.Empty).Replace("\n", string.Empty));
    }
    
    [Fact]
    public async Task WhenSameLineIsCompany1Line_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "Application Vesting Licence Issued November 2017 011 10045454.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);        

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(3, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(9, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Philip John Hobbs", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(5, abstractionLimitsSection.Text!.Count);
        Assert.Single(abstractionLimitsSection.SubResults);

        var sectionPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(sectionPoint1.SubResults);
        
        var sectionPoint1Sub1 = sectionPoint1.SubResults[0];
        Assert.Equal(9, sectionPoint1Sub1.SubResults.Count);

        Assert.Equal("32", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("231", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("4623", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per month") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per month") == true)?.Text!.First().Text);
        Assert.Equal("13870", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text?.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);        
        Assert.Equal("16/51/007/S/011", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService, NaldDataLookupService)).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(primaryLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task W1henSameLineIsCompany1Line_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        // Arrange

        const string filename = "22705032__Application Normal Variation Licence Issued 20062025.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 3);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);        

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(9, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(18, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Yorkshire Water Services Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(11, abstractionLimitsSection.Text!.Count);
        Assert.Equal(2, abstractionLimitsSection.SubResults.Count);

        var sectionPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(sectionPoint1.SubResults);
        
        var sectionPoint1Sub1 = sectionPoint1.SubResults[0];
        Assert.Equal(3, sectionPoint1Sub1.SubResults.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);        
        Assert.Equal("2/27/05/032", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder),
            AbsLicCacheService, NaldDataLookupService)).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal(2, primaryLicence.LinkedLicences.Length);

        Assert.Equal(2, primaryLicence.AbstractionLimits?.Individual?.Length);
        Assert.Equal("6.1", primaryLicence.AbstractionLimits!.Individual![0].DocumentIdentifier);
        Assert.NotNull(primaryLicence.AbstractionLimits.Individual[0].ContainedIn);
        Assert.Single(primaryLicence.AbstractionLimits.Individual[0].ContainedIn!);
        Assert.Equal(InformationSource.Document, primaryLicence.AbstractionLimits.Individual[0].ContainedIn![0].Source);

        Assert.Equal("9.3", primaryLicence.AbstractionLimits.Individual[1].DocumentIdentifier);
        Assert.NotNull(primaryLicence.AbstractionLimits.Individual[1].ContainedIn);
        Assert.Single(primaryLicence.AbstractionLimits.Individual[1].ContainedIn!);
        Assert.Equal(InformationSource.Document, primaryLicence.AbstractionLimits.Individual[1].ContainedIn![0].Source);
        
        Assert.Equal(2, primaryLicence.AbstractionLimits.Aggregates!.Length);
        Assert.Equal("6.2", primaryLicence.AbstractionLimits.Aggregates[0].DocumentIdentifier);
        Assert.NotNull(primaryLicence.AbstractionLimits.Aggregates[0].ContainedIn);
        Assert.Single(primaryLicence.AbstractionLimits.Aggregates[0].ContainedIn!);
        Assert.Equal(InformationSource.Document, primaryLicence.AbstractionLimits.Aggregates[0].ContainedIn![0].Source);
        Assert.Equal("9.4", primaryLicence.AbstractionLimits.Aggregates[1].DocumentIdentifier);
        Assert.NotNull(primaryLicence.AbstractionLimits.Aggregates[1].ContainedIn);
        Assert.Single(primaryLicence.AbstractionLimits.Aggregates[1].ContainedIn!);
        Assert.Equal(InformationSource.Document, primaryLicence.AbstractionLimits.Aggregates[1].ContainedIn![0].Source);
    }
}