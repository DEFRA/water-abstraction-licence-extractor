using System.Text.Json;
using Meziantou.Xunit;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tests.Helper;
using LicenceType = WALE.ProcessFile.Core.Enums.LicenceType;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[EnableParallelization]
[Collection("First Names 1")]
public class PdfPigNoOcrPdfTests(SingletonFirstNamesFixture firstNamesFixture)
{
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider =
        new(TestConfig.PostgresHost,
            TestConfig.PostgresPort,
            TestConfig.PostgresDbName,
            TestConfig.PostgresUsername,
            TestConfig.PostgresPassword);
    
    private static IDatabaseReadService ReadService =>
        new PostgresReadService(NpgsqlDataSourceProvider);

    private static readonly ICacheService DatabaseCacheService =
        new DatabaseCacheService(ReadService, null!);
    
    private Task SetupLicenceNumbersAsync(short regionCode)
    {
        return firstNamesFixture.SetupLicenceNumbersAsync(regionCode, DatabaseCacheService);
    }

    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            // TODO mock of an OCR service that errors if called
        },
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService);
    
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
                        AsrcCode = "G"
                    }
                ]
            },
            {
                "1|2568001248",
                [
                    new NaldData
                    {
                        AsrcCode = "S"
                    }
                ]
            },
            {
                "1|2568001249",
                [
                    new NaldData
                    {
                        AsrcCode = "S"
                    }
                ]
            }
        };

        return returnList;
    }

    private async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode, int fileLicenceMapping, string pdfFolder)
    {
        return new LookupConfiguration(
            WalLabelConfiguration.GetLabels(),
            fileLicenceMapping == 1 ? FileLicenceMapping : FileLicenceMappingWithout52,
            [],
            await firstNamesFixture.FirstNamesCsvTask(),
            new LocalFileService(pdfFolder),
            CacheService,
            regionCode);
    }
    
    private async Task<MatchesResult> GetMatchesAsync(string fileName, int regionCode, int folderNumber = 1, int fileLicenceMapping = 1)
    {
        var pdfFolder = folderNumber == 1 ? TestConfig.PdfFolder : TestConfig.PdfFolder2;
        if (folderNumber == 3) pdfFolder = TestConfig.PdfFolder3;
        if (folderNumber == 5) pdfFolder = TestConfig.PdfFolder5;
        
        return await _pdfDataExtractor.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            await LookupConfigurationAsync(regionCode, fileLicenceMapping, pdfFolder),
            [fileName],
            0);
    }
    
    [Fact]
    public async Task WhenX_NotCheckingAbstractionLimits_ThenFoundCorrectly_IncludesAgreedSchema()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
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
        Assert.Equal(16, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Ingleby Greenhow Water Society Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        Assert.Equal(59, nameResult.LineNumber);
        
        // Note no other licence mentioned
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(4, abstractionLimitsSection.Text!.Count);
        Assert.Equal("A day means any period of 24 consecutive hours and a year means the", abstractionLimitsSection.Text![2].Text);
        Assert.Equal(109, abstractionLimitsSection.LineNumber);
        
        Assert.NotNull(abstractionLimitsSection.SubResults);
        Assert.Single(abstractionLimitsSection.SubResults);
        
        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint1.SubResults);

        var point1Sub1 = abstractionLimitsPoint1.SubResults[0];
        Assert.NotNull(point1Sub1);
        Assert.Equal("AbstractionLimitPointSub", point1Sub1.MatchedLabel?.Name);
        
        Assert.Equal(4, point1Sub1.Text!.Count);

        Assert.NotNull(point1Sub1.SubResults);
        Assert.Equal(5, point1Sub1.SubResults.Count);

        var perDay = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(109, perDay.LineNumber);
        Assert.Equal("90.91", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        var perYear = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("33182", perYear);
        
        var perYearUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("1/25/04/059", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(53, licenceNumberResult.LineNumber);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");

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
        
        Assert.Equal("Purpose", firstPurpose.MatchedLabel!.Name);
        Assert.Equal("4.1 Private Water Supply.", firstPurpose.Text!.First().Text);
        Assert.Equal(2, firstPurpose.SubResults.Count);
        
        var firstPurposeWithoutPrepoint = firstPurpose.SubResults[1];
        Assert.Equal("Private Water Supply", firstPurposeWithoutPrepoint.Text!.First().Text);
        
        var secondPurpose = firstPurposePointGroup.SubResults[1];
        Assert.Equal("4.2 Agriculture (other than Spray Irrigation).", secondPurpose.Text!.First().Text);        
        
        var secondPurposeWithoutPrepoint = secondPurpose.SubResults[1];
        Assert.Equal("Agriculture (other than Spray Irrigation)", secondPurposeWithoutPrepoint.Text!.First().Text);

        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder));
        
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
        Assert.Null(limit.Purposes);
        
        limit = limitG.Limits[1];
        Assert.Equal(LimitPeriodType.PerYear, limit.PeriodType);
        Assert.Equal("cubic metres", limit.Units);
        Assert.Equal(33182, limit.Value);
        Assert.Null(limit.Points);
        Assert.Null(limit.Purposes);
        
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
        await SetupLicenceNumbersAsync(1);
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
        Assert.Equal(6, abstractionLimitsResult.Text!.Count);
        Assert.Equal(111, abstractionLimitsResult.LineNumber);
        
        Assert.NotNull(abstractionLimitsResult.SubResults);        
        Assert.Equal(2, abstractionLimitsResult.SubResults.Count);
        Assert.Equal(111, abstractionLimitsResult.LineNumber);
        
        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal(2, abstractionLimitsSection1.Text!.Count);

        Assert.NotNull(abstractionLimitsSection1.SubResults);
        
        Assert.Single(abstractionLimitsSection1.SubResults);
        var section1Sub1 = abstractionLimitsSection1.SubResults[0];
        Assert.Equal(4, section1Sub1.SubResults.Count);

        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(112, perDay.LineNumber);
        Assert.Equal("205", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perHour = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("41", perHour);
        
        var perHourUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsResult.SubResults[1];
        Assert.Equal(4, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        
        Assert.Single(abstractionLimitsSection2.SubResults);
        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        
        Assert.Equal(6, section2Sub1.SubResults.Count);  
        
        var perYear1 = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("6138", perYear1);
        
        var perYearUnits1 = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits1);
        
        var perYear2 = section2Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("6138", perYear2);
        
        var perYearUnits2 = section2Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits2);        

        var pointsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.Equal(4, pointsResult?.Text!.Count);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");

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

        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();
        
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
        Assert.Null(limit.Purposes);

        limit = limitG.Limits[1];
        Assert.Equal(LimitPeriodType.PerDay, limit.PeriodType);
        Assert.Equal("cubic metres", limit.Units);
        Assert.Equal(205, limit.Value);
        Assert.Null(limit.Points);
        Assert.Null(limit.Purposes);
        
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
        await SetupLicenceNumbersAsync(1);
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
        Assert.Equal(143, abstractionLimitsResult.LineNumber);
        
        Assert.NotNull(abstractionLimitsResult.SubResults);       
        
        Assert.Equal(2, abstractionLimitsResult.SubResults.Count);
        Assert.Equal(143, abstractionLimitsResult.LineNumber);
        
        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal(4, abstractionLimitsSection1.Text!.Count);

        Assert.NotNull(abstractionLimitsSection1.SubResults);
        Assert.Single(abstractionLimitsSection1.SubResults);
        
        var section1Sub1 = abstractionLimitsSection1.SubResults[0];
        
        Assert.Equal(8, section1Sub1.SubResults.Count);
        
        var perHour = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("39.5", perHour);
        
        var perHourUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(144, perDay.LineNumber);
        Assert.Equal("948", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perYear = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per year")));

        Assert.NotNull(perYear);
        Assert.Equal(145, perYear.LineNumber);
        Assert.Equal("40000", perYear.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsResult.SubResults[1];
        Assert.Equal(13, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(12, section2Sub1.SubResults.Count);
            
        perHour = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("39.5", perHour);
        
        perHourUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(152, perDay.LineNumber);
        Assert.Equal("948", perDay.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perYearList = section2Sub1.SubResults
            .Where(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))
            .ToList();
        
       var perYear2 = perYearList.FirstOrDefault()?.Text?.FirstOrDefault()?.Text;
       Assert.Equal("40000", perYear2);
       
       perYear2 = perYearList.LastOrDefault()?.Text?.FirstOrDefault()?.Text;
       Assert.Equal("40000", perYear2); // TODO check value
        
        perYearUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var perSecond = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("10.97", perSecond);
        
        var perSecondUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
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
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

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
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences[0];
        
        Assert.Equal(3, primaryLicence.LinkedLicences.Length);

        Assert.Equal("NW/069/0025/006/R01", primaryLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, primaryLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("AdditionalInformation", primaryLicence.LinkedLicences[0].ContainedIn![1].SectionName);
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
        await SetupLicenceNumbersAsync(1);
        const string filename = "Application new Issued licence 04052017 AN0300012011 9781525.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
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
        Assert.Equal(122, abstractionLimitsSection.LineNumber);
        
        Assert.NotNull(abstractionLimitsSection.SubResults);        
        Assert.Equal(2, abstractionLimitsSection.SubResults.Count);

        var sectionPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(sectionPoint1.SubResults);
        
        var section1Sub1 = sectionPoint1.SubResults[0];
        Assert.Equal(8, section1Sub1.SubResults.Count);
        Assert.Equal(122, section1Sub1.LineNumber);
        
        //var abstractionLimitsSection1 = section1Sub1.SubResults[0];
        Assert.Equal(4, section1Sub1.Text!.Count);

        Assert.NotNull(section1Sub1.SubResults);
        Assert.Equal(8, section1Sub1.SubResults.Count);
        
        var perHour = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("55", perHour);
        
        var perHourUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(123, perDay.LineNumber);
        Assert.Equal("409.5", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perYear = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per year")));

        Assert.NotNull(perYear);
        Assert.Equal(124, perYear.LineNumber);
        Assert.Equal("20457", perYear.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);

        var perSecond = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per second")));

        Assert.NotNull(perSecond);
        Assert.Equal(125, perSecond.LineNumber);
        Assert.Equal("15.2", perSecond.Text?.FirstOrDefault()?.Text);
            
        var perSecondUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(8, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        
        var perYear2 = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("22730", perYear2);
        
        perYearUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var linkedLicenceNumber = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("4/30/12/*G/0214", linkedLicenceNumber);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

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
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences[0];
        
        Assert.Single(primaryLicence.LinkedLicences);

        Assert.Equal("4/30/12/*G/0214", primaryLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(4, primaryLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("FurtherConditions", primaryLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("SimultaneousCompensatoryDischargeCondition", primaryLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal("AdditionalInformation", primaryLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![2].LinkReason);
        Assert.Equal("Purposes", primaryLicence.LinkedLicences[0].ContainedIn![3].SectionName);
        Assert.Equal("CompensatoryDischargeCondition", primaryLicence.LinkedLicences[0].ContainedIn![3].LinkReason);        
    }
    
    [Fact]
    public async Task EWPorterAndSon_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
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
        Assert.Equal(49, abstractionLimitsSection.Text!.Count);
        
        Assert.NotNull(abstractionLimitsSection.SubResults);
        Assert.Equal(10, abstractionLimitsSection.SubResults.Count);
        Assert.Equal(141, abstractionLimitsSection.LineNumber);
        
        var point1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(point1.SubResults);
        Assert.Equal(3, point1.Text!.Count);

        var point1Sub1 = point1.SubResults[0];
        Assert.NotNull(point1Sub1.SubResults);
        Assert.Equal(5, point1Sub1.SubResults.Count);

        var abstractionPoint = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "PointCondition")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("Abstraction Point A", abstractionPoint);
        
        var perHour = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("46", perHour);
        
        var perHourUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        var perSecond = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("12.7", perSecond);
        
        var perSecondUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(3, abstractionLimitsSection2.Text!.Count);

        Assert.Single(abstractionLimitsSection2.SubResults);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
            
        Assert.NotNull(section2Sub1.SubResults);            
        Assert.Equal(5, section2Sub1.SubResults.Count);
        
        abstractionPoint = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "PointCondition")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("Abstraction Point B", abstractionPoint);
        
        perHour = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("46", perHour);
        
        perHourUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("19.2", perSecond);
        
        perSecondUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection3 = abstractionLimitsSection.SubResults[2];
        Assert.Equal(3, abstractionLimitsSection3.Text!.Count);

        Assert.NotNull(abstractionLimitsSection3.SubResults);
        Assert.Single(abstractionLimitsSection3.SubResults);
        
        var section3Sub1 = abstractionLimitsSection3.SubResults[0];
        Assert.Equal(4, section3Sub1.SubResults.Count);
        
        perHour = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("69", perHour);
        
        perHourUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("19.2", perSecond);
        
        perSecondUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection4 = abstractionLimitsSection.SubResults[3];
        Assert.Equal(3, abstractionLimitsSection4.Text!.Count);

        Assert.NotNull(abstractionLimitsSection4.SubResults);
        Assert.Single(abstractionLimitsSection4.SubResults);

        var section4Sub1 = abstractionLimitsSection4.SubResults[0];
        Assert.Equal(4, section4Sub1.SubResults.Count);
        
        perHour = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("137", perHour);
        
        perHourUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("38.1", perSecond);
        
        perSecondUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection5 = abstractionLimitsSection.SubResults[4];
        Assert.Equal(3, abstractionLimitsSection5.Text!.Count);

        Assert.NotNull(abstractionLimitsSection5.SubResults);
        Assert.Single(abstractionLimitsSection5.SubResults);

        var section5Sub1 = abstractionLimitsSection5.SubResults[0];
        Assert.Equal(4, section5Sub1.SubResults.Count);
        
        perHour = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("69", perHour);
        
        perHourUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("19.2", perSecond);
        
        perSecondUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection6 = abstractionLimitsSection.SubResults[5];
        Assert.Equal(3, abstractionLimitsSection6.Text!.Count);

        Assert.NotNull(abstractionLimitsSection6.SubResults);
        Assert.Single(abstractionLimitsSection6.SubResults);

        var section6Sub1 = abstractionLimitsSection6.SubResults[0];
        Assert.Equal(4, section6Sub1.SubResults.Count);
        
        perHour = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("91", perHour);
        
        perHourUnits = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("25.3", perSecond);
        
        perSecondUnits = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection7 = abstractionLimitsSection.SubResults[6];
        Assert.Equal(5, abstractionLimitsSection7.Text!.Count);

        Assert.NotNull(abstractionLimitsSection7.SubResults);
        Assert.Single(abstractionLimitsSection7.SubResults);

        var section7Sub1 = abstractionLimitsSection7.SubResults[0];
        Assert.Equal(5, section7Sub1.SubResults.Count);
        
        var perDay = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("1440", perDay);
        
        var perDayUnits = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        var perYear = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("22862", perYear);
        
        var perYearUnits = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);                                
        
        var abstractionLimitsSection8 = abstractionLimitsSection.SubResults[7];
        Assert.Equal(7, abstractionLimitsSection8.Text!.Count);

        Assert.NotNull(abstractionLimitsSection8.SubResults);
        Assert.Single(abstractionLimitsSection8.SubResults);

        var section8Sub1 = abstractionLimitsSection8.SubResults[0];
        Assert.Equal(7, section8Sub1.SubResults.Count);
        
        perHour = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("251", perHour);
        
        perHourUnits = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("4091", perDay);
        
        perDayUnits = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        perYear = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("190000", perYear);
        
        perYearUnits = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
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
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("46", perHour);
        
        perHourUnits = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("1091", perDay);
        
        perDayUnits = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        perYear = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("40900", perYear);
        
        perYearUnits = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        perYear = section9Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("40900", perYear);
        
        perYearUnits = section9Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
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
        Assert.Equal(9, section10Sub1.SubResults.Count);
        
        perHour = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("205", perHour);
        
        perHourUnits = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("3000", perDay);
        
        perDayUnits = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        perYear = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("190000", perYear);
        
        perYearUnits = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);                
        
        linkedLicenceNumber = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("6/33/56/*G/0274/R02", linkedLicenceNumber);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);        
        Assert.Equal("AN/033/0051/004", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

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
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();
        
        var primaryLicence = agreedSchemaLicenceGroup.Licences[0];
        
        Assert.Single(primaryLicence.LinkedLicences);

        Assert.Equal("6/33/56/*G/0274/R02", primaryLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(primaryLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }

    [Fact]
    public async Task WalderseyFarmsLimited_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
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
        Assert.Equal(163, abstractionLimitsSection.LineNumber);
        
        var section1Point1 = abstractionLimitsSection.SubResults[0];
        Assert.Equal(4, section1Point1.Text!.Count);
        Assert.NotNull(section1Point1.SubResults);
        Assert.Single(section1Point1.SubResults);
        
        var point1Sub1 = section1Point1.SubResults[0];
        Assert.Equal(6, point1Sub1.SubResults.Count);

        var perDay = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(165, perDay.LineNumber);
        Assert.Equal("2000", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perHour = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("83", perHour);
        
        var perHourUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var perSecond = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per second")));

        Assert.NotNull(perSecond);
        Assert.Equal(166, perSecond.LineNumber);
        Assert.Equal("23.1", perSecond.Text?.FirstOrDefault()?.Text);
            
        var perSecondUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(4, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(2, section2Sub1.SubResults.Count);
        
        var perYear = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per year")));

        Assert.NotNull(perYear);
        Assert.Equal(169, perYear.LineNumber);
        Assert.Equal("68000", perYear.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var abstractionLimitsSection3 = abstractionLimitsSection.SubResults[2];
        Assert.Equal(2, abstractionLimitsSection3.Text!.Count);

        Assert.NotNull(abstractionLimitsSection3.SubResults);
        Assert.Single(abstractionLimitsSection3.SubResults);

        var section3Sub1 = abstractionLimitsSection3.SubResults[0];
        Assert.Equal(2, section3Sub1.SubResults.Count);
        
        perYear = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per year")));

        Assert.NotNull(perYear);
        Assert.Equal(173, perYear.LineNumber);
        Assert.Equal("68000", perYear.Text?.FirstOrDefault()?.Text);
        
        perYearUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var abstractionLimitsSection4 = abstractionLimitsSection.SubResults[3];
        Assert.Equal(5, abstractionLimitsSection4.Text!.Count);

        Assert.NotNull(abstractionLimitsSection4.SubResults);
        Assert.Single(abstractionLimitsSection4.SubResults);

        var section4Sub1 = abstractionLimitsSection4.SubResults[0];
        Assert.Equal(8, section4Sub1.SubResults.Count);

        perHour = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("219", perHour);
        
        perHourUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var perYearList = section4Sub1.SubResults
            .Where(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per year")))
            .ToList();

        perYear = perYearList.FirstOrDefault();
        
        Assert.NotNull(perYear);
        Assert.Equal(177, perYear.LineNumber);
        Assert.Equal("61200", perYear.Text?.FirstOrDefault()?.Text);
        
        perYear = perYearList.LastOrDefault();
        
        Assert.NotNull(perYear);
        Assert.Equal(177, perYear.LineNumber);
        Assert.Equal("61200", perYear.Text?.FirstOrDefault()?.Text);

        Assert.Equal(1, section4Sub1.SubResults
            .Count(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year"))));
        
        perYearUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);   
        
        perDay = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(176, perDay.LineNumber);
        Assert.Equal("5256", perDay.Text?.FirstOrDefault()?.Text);

        perDayUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var abstractionLimitsSection5 = abstractionLimitsSection.SubResults[4];
        Assert.Equal(11, abstractionLimitsSection5.Text!.Count);

        Assert.NotNull(abstractionLimitsSection5.SubResults);
        Assert.Single(abstractionLimitsSection5.SubResults);

        var section5Sub1 = abstractionLimitsSection5.SubResults[0];
        Assert.Equal(10, section5Sub1.SubResults.Count);

        perYearList = section5Sub1.SubResults
            .Where(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per year")))
            .ToList();

        perYear = perYearList.FirstOrDefault();
        
        Assert.NotNull(perYear);
        Assert.Equal(185, perYear.LineNumber);
        Assert.Equal("68000", perYear.Text?.FirstOrDefault()?.Text);
        
        perYear = perYearList.LastOrDefault();
        
        Assert.NotNull(perYear);
        Assert.Equal(185, perYear.LineNumber);
        Assert.Equal("68000", perYear.Text?.FirstOrDefault()?.Text);
        
        perYearUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        perHour = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("219", perHour);
        
        perHourUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);                        
        
        perDay = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(184, perDay.LineNumber);
        Assert.Equal("5256", perDay.Text?.FirstOrDefault()?.Text);

        perDayUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var linkedLicenceNumber = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("AN/033/0047/018", linkedLicenceNumber);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");  

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
        Assert.Equal("Purpose", purpose1.MatchedLabel!.Name);
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
        Assert.Equal("Purpose", purpose2.MatchedLabel!.Name);
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
        Assert.Equal("Purpose", purpose3.MatchedLabel!.Name);
       
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
        
        Assert.Equal(53, pointsResult.Text!.Count);
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
        Assert.Equal("Between National Grid References TL 55782 94571 and TL 55844 94741",
            string.Join(' ', point1TTextWithoutPurposeAndPoint.Text?.Select(x => x.Text).ToArray()!));
        
        var pointPurposeGroup2 = pointsResult.SubResults[1];
        Assert.Equal("PointPurposeGroup", pointPurposeGroup2.MatchedLabel!.Name);
        Assert.Equal(50, pointPurposeGroup2.Text!.Count);
        
        var pointPurposeGroup2Text = pointPurposeGroup2.Text!;

        Assert.Equal(50, pointPurposeGroup2Text.Count);
        Assert.Equal("2.2 For Purpose 4.3", pointPurposeGroup2Text[0].Text);
        Assert.Equal("National Grid References", pointPurposeGroup2Text[1].Text);
        Assert.Equal("From To", pointPurposeGroup2Text[2].Text);
        Assert.Equal("TL5584494741 TL5453692523", pointPurposeGroup2Text[3].Text);
        //...
        Assert.Equal("TL5616889665 TL5658389810", pointPurposeGroup2Text[49].Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        Assert.NotNull(agreedSchemaLicenceGroup.Licences);
        Assert.Equal(2, agreedSchemaLicenceGroup.Licences.Length);
        
        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Equal(filename, primaryLicence.Filename);
        Assert.Equal("6/33/47/*S/0172/R01", primaryLicence.LicenceNumber?.Value);

        var points = primaryLicence.Points;
        Assert.Equal(2, points.Length);
        
        var primaryPoint1 = points[0];
        Assert.Equal("2.1", primaryPoint1.Id);
        Assert.Equal("Between National Grid References TL 55782 94571 and TL 55844 94741", primaryPoint1.Description);
        Assert.Equal(2, primaryPoint1.PurposeIds!.Length);
        Assert.Equal("4.1", primaryPoint1.PurposeIds[0]);
        Assert.Equal("4.2", primaryPoint1.PurposeIds[1]);
        
        var primaryPoint2 = points[1];
        Assert.Equal("2.2", primaryPoint2.Id);
        Assert.Equal(1242, primaryPoint2.Description!.Length);
        Assert.StartsWith("National Grid References From To TL558449", primaryPoint2.Description);
        Assert.Single(primaryPoint2.PurposeIds!);
        Assert.Equal("4.3", primaryPoint2.PurposeIds![0]);

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
        await SetupLicenceNumbersAsync(1);
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
        Assert.Equal(4, point1Sub1.SubResults.Count);

        var perDay = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(155, perDay.LineNumber);
        Assert.Equal("2500", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perSecond = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("29", perSecond);
        
        var perSecondUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(3, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(4, section2Sub1.SubResults.Count);
        
        perDay = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(158, perDay.LineNumber);
        Assert.Equal("5000", perDay.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        perSecond = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("57.9", perSecond);
        
        perSecondUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection3 = abstractionLimitsSection.SubResults[2];
        Assert.Equal(3, abstractionLimitsSection3.Text!.Count);

        Assert.NotNull(abstractionLimitsSection3.SubResults);
        Assert.Single(abstractionLimitsSection3.SubResults);

        var section3Sub1 = abstractionLimitsSection3.SubResults[0];
        Assert.Equal(4, section3Sub1.SubResults.Count);

        perDay = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(161, perDay.LineNumber);
        Assert.Equal("5000", perDay.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        perSecond = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("57.9", perSecond);
        
        perSecondUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection4 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(3, abstractionLimitsSection4.Text!.Count);

        Assert.NotNull(abstractionLimitsSection4.SubResults);
        Assert.Single(abstractionLimitsSection4.SubResults);

        var section4Sub1 = abstractionLimitsSection4.SubResults[0];
        Assert.Equal(4, section4Sub1.SubResults.Count);
        
        perDay = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(158, perDay.LineNumber);
        Assert.Equal("5000", perDay.Text?.FirstOrDefault()?.Text); // TODO there are 2 5000s and 1 5300
        
        perDayUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        perSecond = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("57.9", perSecond); // TODO there is also 61.3
        
        perSecondUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        // TODO 4 more sections
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(primaryLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenNearNextLineIsCompany_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
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
        Assert.NotNull(resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose"));
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

        Assert.Equal(27, abstractionLimitsSection.Text!.Count);
        
        Assert.Equal(4, abstractionLimitsSection.SubResults.Count);
        var sectionPoint1 = abstractionLimitsSection.SubResults[0];

        Assert.Single(sectionPoint1.SubResults);

        var sectionPoint1Sub1 = sectionPoint1.SubResults[0];
        Assert.Equal(8, sectionPoint1Sub1.SubResults.Count);
        Assert.Single(sectionPoint1Sub1.SubResults[0].Text!);
        
        var sectionPoint2 = abstractionLimitsSection.SubResults[1];

        Assert.Single(sectionPoint2.SubResults);

        var sectionPoint2Sub1 = sectionPoint2.SubResults[0];
        Assert.Equal(8, sectionPoint2Sub1.SubResults.Count);
        Assert.Single(sectionPoint2Sub1.SubResults[0].Text!);
        
        var sectionPoint3 = abstractionLimitsSection.SubResults[2];

        Assert.Single(sectionPoint3.SubResults);

        var sectionPoint3Sub1 = sectionPoint3.SubResults[0];
        Assert.Equal(8, sectionPoint3Sub1.SubResults.Count);
        Assert.Single(sectionPoint3Sub1.SubResults[0].Text!);
        
        var sectionPoint4 = abstractionLimitsSection.SubResults[3];

        Assert.Single(sectionPoint4.SubResults);

        var sectionPoint4Sub1 = sectionPoint4.SubResults[0];
        Assert.Equal(8, sectionPoint4Sub1.SubResults.Count);
        Assert.Single(sectionPoint4Sub1.SubResults[0].Text!);
        
        // TODO expand this section + add others
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(primaryLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenNearPreviousLineIsCompany_SimpleAbstractionLimits1LicenceToLicenceLink_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "Application Minor Variation Issued Licence 11.12.2019 11149448.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, fileLicenceMapping: 2);
        var resultList = resultFull.Matches!;
        
        // Assert
        
        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);  
        
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(10, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(32, additionalInformation.Text!.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Rolawn Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.NotNull(abstractionLimitsSection.Text);
        Assert.Equal(11, abstractionLimitsSection.Text.Count);
        Assert.Equal("200,000 cubic metres per year.", 
        abstractionLimitsSection.Text![10].Text);
        Assert.Equal(2, abstractionLimitsSection.SubResults.Count);
        Assert.Equal(7, abstractionLimitsSection.SubResults[0].Text!.Count);

        var point1 = abstractionLimitsSection.SubResults[0];
        var point1Sub1 = point1.SubResults[0];
        
        Assert.Equal("120", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("2600", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("60000", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("33.3", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text!.First().Text);
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text!.First().Text);
        Assert.Equal("60000", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);
        
        // TODO
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");   
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("NE/027/0028/059", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 2, TestConfig.PdfFolder))).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Single(primaryLicence.LinkedLicences);

        Assert.Equal("NE/026/0034/052", primaryLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, primaryLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("FurtherConditions", primaryLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", primaryLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal("AdditionalInformation", primaryLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("AggregateConditions", primaryLicence.LinkedLicences[0].ContainedIn![2].LinkReason);
    }
    
    [Fact]
    public async Task XXXWhenSameLineIsCompany1Line_AndAbstractionLimitsToBeFoundWithSpellingMistake_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893476.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        //Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

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
        Assert.Equal(9, abstractionLimits.Text!.Count);
        Assert.Equal("The aggregate quality of water authorised to be abstracted under this licence", abstractionLimits.Text![3].Text);
        Assert.Single(abstractionLimits.SubResults);

        var abstractionLimitsPoint = abstractionLimits.SubResults[0];
        Assert.Equal(2, abstractionLimitsPoint.SubResults.Count); // TODO should investigate this later if this should be 2 or 3
        
        var abstractionLimitPointSub1 = abstractionLimitsPoint.SubResults[0];
        
        Assert.Equal("20", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("475", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
            && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("173453", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", abstractionLimitPointSub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
            && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);

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
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        Assert.NotNull(agreedSchemaLicenceGroup.Licences);
        Assert.Equal(3, agreedSchemaLicenceGroup.Licences.Length);
        
        Assert.Equal("25/68/001/249", agreedSchemaLicenceGroup.Licences[0].LicenceNumber?.Value);
        Assert.Null(agreedSchemaLicenceGroup.Licences[0].DmsPath);
        Assert.Equal(LicenceStatus.Ok, agreedSchemaLicenceGroup.Licences[0].Status);
        Assert.Equal(Core.Enums.OutputSchema.LicenceType.SurfaceWaterAbstraction, agreedSchemaLicenceGroup.Licences[0].LicenceType);
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
        Assert.Equal(Core.Enums.OutputSchema.LicenceType.GroundWaterAbstraction, primaryLicence.LinkedLicences[0].LicenceType);
        Assert.Equal(NaldLicenceStatus.Live, primaryLicence.LinkedLicences[0].NaldStatus);
        
        Assert.Equal(2, primaryLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(LinkedLicenceDirection.Outgoing, primaryLicence.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal(LinkedLicenceSource.Document, primaryLicence.LinkedLicences[0].ContainedIn![0].Source);
        Assert.Equal(LinkedLicenceDirection.Incoming, primaryLicence.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal(LinkedLicenceSource.OtherDocument, primaryLicence.LinkedLicences[0].ContainedIn![1].Source);
        
        Assert.Equal("25/68/001/248", primaryLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, primaryLicence.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(LinkedLicenceDirection.Outgoing, primaryLicence.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        Assert.Equal(LinkedLicenceSource.Document, primaryLicence.LinkedLicences[1].ContainedIn![0].Source);
        Assert.Equal(LinkedLicenceDirection.Incoming, primaryLicence.LinkedLicences[1].ContainedIn![1].Direction);
        Assert.Equal("AbstractionLimits", primaryLicence.LinkedLicences[1].ContainedIn![1].SectionName);
        Assert.Equal("AggregateCondition", primaryLicence.LinkedLicences[1].ContainedIn![1].LinkReason);      
        Assert.Equal(LinkedLicenceSource.OtherDocument, primaryLicence.LinkedLicences[1].ContainedIn![1].Source);
        
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
        Assert.Equal("At National Grid Reference SJ 5179 4988",
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
        agreedSchemaLicenceGroup.Licences[1].LicenceVersion.DmsFileIdStatusDateUtc = new DateTime(2001, 2, 3);
        agreedSchemaLicenceGroup.Licences[2].LicenceVersion.DmsFileIdStatusDateUtc = new DateTime(2001, 2, 3);
        
        var actualJson = JsonSerializer.Serialize(agreedSchemaLicenceGroup, JsonHelper.GetSerializerOptions());
        var expectedJson =
            await File.ReadAllTextAsync("Data/2568001247-LV20190619-2568001248-LV20190619-2568001249-LV20190619.json");
        
        Assert.Equal(
            expectedJson.Replace(" ", string.Empty).Replace("\n", string.Empty),
            actualJson.Replace(" ", string.Empty).Replace("\n", string.Empty));
    }
    
    [Fact]
    public async Task WhenSameLineIsCompany1Line_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
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
        Assert.Equal(4, abstractionLimitsSection.Text!.Count);
        Assert.Single(abstractionLimitsSection.SubResults);

        var sectionPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(sectionPoint1.SubResults);
        
        var sectionPoint1Sub1 = sectionPoint1.SubResults[0];
        Assert.Equal(8, sectionPoint1Sub1.SubResults.Count);

        Assert.Equal("32", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("231", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("4623", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per month") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per month") == true)?.Text!.First().Text);
        Assert.Equal("13870", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);        
        Assert.Equal("16/51/007/S/011", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(primaryLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenObscureCompanyName_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        await SetupLicenceNumbersAsync(1);
        const string filename = "Application NA New Issued Licence 11765926.pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

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
        Assert.Equal(7, abstractionLimitsSection.Text!.Count);

        Assert.Single(abstractionLimitsSection.SubResults);

        var abstractionLimitsPoint = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint.SubResults);
        
        var point1Sub1 = abstractionLimitsPoint.SubResults[0];
        Assert.Equal(9, point1Sub1.SubResults.Count);

        Assert.Equal("2", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("30", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("11000", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("0.6", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text!.First().Text);                
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text!.First().Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("NE/021/0000/036", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(primaryLicence.LinkedLicences);
    }

    [Fact]
    public async Task WhenPersonalNameNoTitle_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        await SetupLicenceNumbersAsync(1);
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
        Assert.Equal(9, abstractionLimitsSection.Text!.Count);
        Assert.Single(abstractionLimitsSection.SubResults);

        var sectionPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(sectionPoint1.SubResults);

        var point1Sub1 = sectionPoint1.SubResults[0];
        Assert.Equal(9, point1Sub1.SubResults.Count);

        Assert.Equal("43.2", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("1037", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text!.First().Text);        
        Assert.Equal("37000", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text!.First().Text);        
        Assert.Equal("12", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text!.First().Text);                
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text!.First().Text);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("4/29/04/*S/0098/R01", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        var primaryLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(primaryLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenMultipleNamesWithNoTitle_And3ConditionsOfAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        await SetupLicenceNumbersAsync(1);
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
        Assert.Equal(65, additionalInformation.Text!.Count);
        
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
        Assert.Equal(25, abstractionLimitsSection.Text!.Count);

        Assert.Equal(3, abstractionLimitsSection.SubResults.Count);

        var point1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(point1.SubResults);

        var point1Sub1 = point1.SubResults[0];
        Assert.Equal(9, point1Sub1.SubResults.Count);

        var pointName = point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition")?.Text!.First().Text;
        
        Assert.Equal("2.1", pointName);
        
        Assert.Equal("90", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
            && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("2160", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);   
        Assert.Equal("113650", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("25.3", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);           

        // TODO add a test for the futher conditions 90,923
        
        Assert.Equal("25.3", point1Sub1.SubResults[8].Text!.First().Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("SO/042/0036/022", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Equal("SO/042/0036/022", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.Equal("SO/042/0036/023", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);

        Assert.Equal("36/134", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("LapsedLicence", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        Assert.Equal("SO/042/0036/024", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task WhenCompanyNameBeforeLabelWhenUsuallyAfter_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
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
        Assert.Equal(9, abstractionLimitsSection.Text!.Count);
        Assert.Single(abstractionLimitsSection.SubResults);

        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint1.SubResults);

        var point1Sub1 = abstractionLimitsPoint1.SubResults[0];
        Assert.Equal(9, point1Sub1.SubResults.Count);
        
        Assert.Equal("3.5", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("30", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("8300", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("0.97", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("SO/040/0009/016", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenX_EveyrhtingFoundButListSayingOtherwise_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
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
        Assert.Equal(27, abstractionLimitsSection.Text!.Count);
        Assert.Equal(4, abstractionLimitsSection.SubResults.Count);

        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint1.SubResults);

        var point1Sub1 = abstractionLimitsPoint1.SubResults[0];
        Assert.Equal(7, point1Sub1.SubResults.Count);
        
        Assert.Equal("14", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("112", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("22731", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        
        // TODO, 3 other points
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("9/40/01/0500/G", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task Z_Z_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Application - formal variation - issue licence 9227047.pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(12, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(17, additionalInformation.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

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
        Assert.Equal(19, abstractionLimitsSection.Text!.Count);
        Assert.Equal(3, abstractionLimitsSection.SubResults.Count);

        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(abstractionLimitsPoint1.SubResults);

        var point1Sub1 = abstractionLimitsPoint1.SubResults[0];
        Assert.Equal(9, point1Sub1.SubResults.Count);

        Assert.Equal("6.1 Up to and including 31 March 2025", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Date"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("Up to and including ") == true)?.Text![0].Text);
        
        Assert.Equal("215", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("4550", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("1460000", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("59.7", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("per second") == true)?.Text![0].Text);
        
        var abstractionLimitsPoint2 = abstractionLimitsSection.SubResults[1];
        Assert.Single(abstractionLimitsPoint2.SubResults);
        
        var point2Sub1 = abstractionLimitsPoint2.SubResults[0];
        Assert.Equal(9, point2Sub1.SubResults.Count);

        Assert.Equal("6.2 From 01 April 2025", point2Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Date"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Text.Contains("From ") == true)?.Text![0].Text);
        
        var abstractionLimitsPoint3 = abstractionLimitsSection.SubResults[2];
        Assert.Single(abstractionLimitsPoint3.SubResults);
        
        var point3Sub1 = abstractionLimitsPoint3.SubResults[0];
        Assert.Equal(7, point3Sub1.SubResults.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("08/37/54/0025", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

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
        await SetupLicenceNumbersAsync(1);
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
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

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
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder));

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
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("DewateringDischargeCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task WhenABCD_DEF_ThenY()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "1.3-licence-07.02.2023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);

        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("South West Water Limited", companyName?.Text?.FirstOrDefault()?.Text);

        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder));
        
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
            agreedSchemaLicence.AbstractionLimits.Aggregates[0].LicenceVersionId);
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
        await SetupLicenceNumbersAsync(1);
        const string filename = "Abstraction Licence 7310604.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        
        var abstractionLimitsResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.False(abstractionLimitsResult.IsOcr);
        Assert.Equal(17, abstractionLimitsResult.Text!.Count);
        Assert.Equal(109, abstractionLimitsResult.LineNumber);
        
        Assert.NotNull(abstractionLimitsResult.SubResults);        
        Assert.Equal(3, abstractionLimitsResult.SubResults.Count);
        Assert.Equal(109, abstractionLimitsResult.LineNumber);
        
        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal(4, abstractionLimitsSection1.Text!.Count);
        Assert.NotNull(abstractionLimitsSection1.SubResults);
        Assert.Single(abstractionLimitsSection1.SubResults);
        var section1Sub1 = abstractionLimitsSection1.SubResults[0];
        Assert.Equal(8, section1Sub1.SubResults.Count);
        
        var abstractionLimitsSection2 = abstractionLimitsResult.SubResults[1];
        Assert.Equal(4, abstractionLimitsSection2.Text!.Count);
        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults);
        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(8, section2Sub1.SubResults.Count);
        
        var abstractionLimitsSection3 = abstractionLimitsResult.SubResults[2];
        Assert.Equal(7, abstractionLimitsSection3.Text!.Count); // TODO should really be 5, its including a header from the next page
        Assert.NotNull(abstractionLimitsSection3.SubResults);
        Assert.Single(abstractionLimitsSection3.SubResults);
        var section3Sub1 = abstractionLimitsSection3.SubResults[0];
        Assert.Equal(5, section3Sub1.SubResults.Count);

        Assert.Equal("cubic metres", section3Sub1.SubResults[0].Text!.FirstOrDefault()!.Text);
        Assert.Equal("cubic metres", section3Sub1.SubResults[1].Text!.FirstOrDefault()!.Text);
        Assert.Equal("15", section3Sub1.SubResults[2].Text!.FirstOrDefault()!.Text);
        Assert.Equal("360", section3Sub1.SubResults[3].Text!.FirstOrDefault()!.Text);
        Assert.Equal("1 January and ending on 31 December", section3Sub1.SubResults[4].Text!.FirstOrDefault()!.Text);
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.Equal(2, points!.Text!.Count);
        Assert.Equal("2.1. At National Grid Reference TA 04990 38509 at the point marked \"A\" on the", points.Text![0].Text);
        Assert.Equal("map.", points.Text![1].Text);
        
        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("Lakeminster Park Limited", companyName?.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();
        
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
        await SetupLicenceNumbersAsync(1);
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
        
        var purposes = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Purpose");
        Assert.NotNull(purposes);

        var purposesSub = purposes.SubResults;
        Assert.Single(purposesSub);
        
        Assert.Equal(2, purposesSub[0].SubResults
            .Where(sr => sr.MatchedLabel?.Name == "Purpose")
            .ToList()
            .Count);
        
        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("Yorkshire", companyName?.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();
        
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
        Assert.EndsWith("National Grid Reference SE 15454 02535", point.Description);
        
        point = agreedSchemaLicence.Points[1];
        Assert.Equal("2.2", point.Id);
        Assert.EndsWith("National Grid Reference SE 15253 01352", point.Description);
        
        point = agreedSchemaLicence.Points[2];
        Assert.Equal("2.3", point.Id);
        Assert.EndsWith("National Grid Reference SE 15820 01918", point.Description);
        
        point = agreedSchemaLicence.Points[3];
        Assert.Equal("2.4", point.Id);
        Assert.EndsWith("National Grid Reference SE 15192 03582", point.Description);
        
        point = agreedSchemaLicence.Points[4];
        Assert.Equal("2.5", point.Id);
        Assert.EndsWith("National Grid Reference SE 13596 03969", point.Description); // TODO should be "E" not "E
        
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
        
        Assert.Single(limit.Purposes!);
        Assert.Equal("4.1", limit.Purposes![0].Id);
        Assert.Equal(38640, limit.Value);

        limit = limitG.Limits[1];
        Assert.Single(limit.Purposes!);
        Assert.Equal("4.1", limit.Purposes![0].Id);
        Assert.Equal(10140000, limit.Value);

        limitG = agreedSchemaLicence.AbstractionLimits.Individual[1];
        limit = limitG.Limits[0];
        
        Assert.Single(limit.Purposes!);
        Assert.Equal("4.2", limit.Purposes![0].Id);
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
        Assert.Equal(10140000, aggregate.Limits[1].Value);
        Assert.Null(aggregate.Limits[1].Purposes);
        Assert.Null(aggregate.Limits[1].Points);
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_YorkshireWaterCompany4_ThenY()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Application Formal Variation Issued Licence 07032023 (1).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        Assert.Equal(9, points.Text!.Count);
        Assert.StartsWith("2.1 At National Grid Reference SE 069 076", points.Text![0].Text);

        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("Yorkshire", companyName?.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();
        
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
        Assert.EndsWith("At National Grid Reference SE 069 076", point.Description);
        
        point = agreedSchemaLicence.Points[1];
        Assert.Equal("2.2", point.Id);
        Assert.EndsWith("At National Grid Reference SE 054 096", point.Description);
        
        point = agreedSchemaLicence.Points[2];
        Assert.Equal("2.3", point.Id);
        Assert.EndsWith("At National Grid Reference SE 047 105", point.Description);
        
        point = agreedSchemaLicence.Points[3];
        Assert.Equal("2.4", point.Id);
        Assert.EndsWith("At National Grid Reference SE 073 115", point.Description);
        
        point = agreedSchemaLicence.Points[4];
        Assert.Equal("2.5", point.Id);
        Assert.EndsWith("At National Grid Reference SE 098 130", point.Description);
        
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
        Assert.Equal(12410000, limit.Value);
        
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_YorkshireWaterCompany5_ThenY()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Application Formal Variation Issued Licence 07032023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        Assert.Equal(22, points.Text!.Count);
        Assert.StartsWith("2.1 At the following National Grid Refe", points.Text![0].Text);

        var pointPurposeGroup = points.SubResults[0];
        var pointsAll = pointPurposeGroup.SubResults.Where(x => x.MatchedLabel?.Name == "Point").ToList();
        
        Assert.Equal(20, pointsAll.Count);
        Assert.StartsWith("A SE 06", pointsAll.First().Text?.FirstOrDefault()?.Text);
        Assert.StartsWith("T SE 02", pointsAll.Last().Text?.FirstOrDefault()?.Text);
        
        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("Yorkshire", companyName?.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();
        
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
        Assert.Null(point.Id);
        Assert.StartsWith("SE 066 152", point.Description);
        Assert.Equal(10, point.Description!.Length);
        
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
        Assert.Null(limit.Points!);
        Assert.Equal(5840000, limit.Value);
        
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_YorkshireWaterCompany6_ThenY()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
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
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();
        
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
        Assert.EndsWith("At National Grid Reference SE 039 152", point.Description);
        
        point = agreedSchemaLicence.Points[1];
        Assert.Equal("2.2", point.Id);
        Assert.EndsWith("At National Grid Reference SE 052 166", point.Description);
        
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
        Assert.Single(limit.Points!);
        Assert.Equal(730000, limit.Value);

        limitG = agreedSchemaLicence.AbstractionLimits.Individual[1];
        limit = limitG.Limits[0];
        
        Assert.Null(limit.Purposes);
        Assert.Single(limit.Points!);
        Assert.Equal(2920000, limit.Value);
        
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_FileThatErrored_ThenY()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Application - Minor Variation -Application New Licence Issued 28_04_2021 00_00_00 11794555.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceNumber = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("18/54/21/0116", licenceNumber.Text!.First().Text);
        
        var records = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(12, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(75, additionalInformation.Text!.Count);
        
        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 1, TestConfig.PdfFolder))).Last();

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
        await SetupLicenceNumbersAsync(1);
        const string filename = "22718045__Application - Reduction -Application New Licence Issued 24_06_2019 00_00_00 10897641.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 2);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var records = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(6, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(19, additionalInformation.Text!.Count);
        
        var issuerResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
            
        var purposeResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal("4. PURPOSE OF ABSTRACTION 4.1 Cooling water make up (68% returned to source).",
            string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!));
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, 2, TestConfig.PdfFolder))).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();

        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_PurposeHasAnUptoInIt_ThenNowGetsThem()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "22719149__Application Formal Variation - Issued Licence [04-09-2018] 10474343.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var records = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(19, records.Text!.Count);
        
        var additionalInformation = resultFull.Matches?.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(37, additionalInformation.Text!.Count);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, 2, TestConfig.PdfFolder2));
        
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
        await SetupLicenceNumbersAsync(4);
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
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(4, 2, TestConfig.PdfFolder2));
        
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
        await SetupLicenceNumbersAsync(3);
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

        var licenceGroups = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, 2, TestConfig.PdfFolder2));
        
        Assert.Equal(3, licenceGroups.Count);

        var agreedSchemaLicenceGroup = licenceGroups[1];
        Assert.Equal(4, agreedSchemaLicenceGroup.Licences.Length);
        
        Assert.Equal("NE/026/0034/052", agreedSchemaLicenceGroup.Licences[0].LicenceNumber?.Value);
        Assert.Equal("NE/027/0028/059", agreedSchemaLicenceGroup.Licences[1].LicenceNumber?.Value);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Equal(3, agreedSchemaLicence.Purposes.Length);
        Assert.Equal("Spray irrigation", agreedSchemaLicence.Purposes[0].Description);
        
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/027/0028/059", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("AggregateConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].LinkReason);

        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("AggregateConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);

        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[2].ContainedIn!.Length);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("ReadInConjunction", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
        Assert.Equal(LinkedLicenceSource.Document, agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].Source);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[2].ContainedIn![1].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[2].ContainedIn![1].LinkReason);
        Assert.Equal(LinkedLicenceSource.Document, agreedSchemaLicence.LinkedLicences[2].ContainedIn![1].Source);
        
        Assert.Equal(4, agreedSchemaLicence.Points.Length);
        Assert.Equal("2.1 A", agreedSchemaLicence.Points[0].Id);
        Assert.Equal("A SE 80360 41490 Southfield Farm, Everingham, York. 1", agreedSchemaLicence.Points[0].Description);
        Assert.Equal("2.1 B", agreedSchemaLicence.Points[1].Id);
        Assert.Equal("B SE 80490 43730 Ponds Farm, Everingham, York. 1", agreedSchemaLicence.Points[1].Description);
        Assert.Equal("2.1 D", agreedSchemaLicence.Points[2].Id);
        Assert.Equal("D SE 70910 39340 Ellerton, East Riding of Yorkshire. 2", agreedSchemaLicence.Points[2].Description);
        Assert.Equal("2.1 E", agreedSchemaLicence.Points[3].Id);
        Assert.Equal("E SE 73917 47832 Low Farm, Sutton Upon Derwent, Yorkshire. 3", agreedSchemaLicence.Points[3].Description);
        
        Assert.Equal(5, agreedSchemaLicence.AbstractionLimits.Individual!.Length);
        Assert.Equal(19, agreedSchemaLicence.AbstractionLimits.Individual!.SelectMany(x => x.Limits).Count());
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Aggregates!.Length);
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Aggregates.SelectMany(x => x.Limits).Count());
    }
    
    [Fact]
    public async Task When_GettingFurtherConditions_ThenNowGetsThem()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
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
        Assert.Equal(39, furtherConditions.Text!.Count);

        Assert.Equal(4, furtherConditions.SubResults.Count);
        Assert.Equal("9.1 (i) No abstraction shall take place unless the Licence Holder has installed a", furtherConditions.SubResults[0].Text!.First().Text);

        Assert.Equal("9.2 No abstraction shall take place when the flow in the Back Delfin as gauged", furtherConditions.SubResults[1].Text!.First().Text);
        Assert.Equal("NE/026/0034/052", furtherConditions.SubResults[1].SubResults[0].Text!.First().Text);
        Assert.Equal("NE/026/0034/053", furtherConditions.SubResults[1].SubResults[1].Text!.First().Text);
        
        Assert.Equal("9.3 Abstraction shall not exceed 2,000 cubic metres per day when the flow in the", furtherConditions.SubResults[2].Text!.First().Text);
        Assert.Equal("NE/026/0034/052", furtherConditions.SubResults[2].SubResults[0].Text!.First().Text);
        Assert.Equal("NE/026/0034/053", furtherConditions.SubResults[2].SubResults[1].Text!.First().Text);
        
        Assert.Equal("9.4 The minimum value for the quantity of water authorised to be abstracted", furtherConditions.SubResults[3].Text!.First().Text);

        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, 1, TestConfig.PdfFolder2));
        
        Assert.Equal(3, licenceSets.Count);
        var agreedSchemaLicenceGroup = licenceSets[1];
        
        Assert.Equal("NE0260034018-LV2019121120250331-NE0260034052-LV2019121120270331-NE0260034053-LVUNKNOWN-NE0260034056-LV2020091020370331",
            agreedSchemaLicenceGroup.LicenceSetId);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.AggregateSets!.Length);
        Assert.Equal(4, agreedSchemaLicenceGroup.Licences.Length);
        
        // For primary licence
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];
        
        Assert.Equal("NE/026/0034/056", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(LicenceStatus.Ok, agreedSchemaLicence.Status);

        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual!);
        Assert.Equal(4, agreedSchemaLicence.AbstractionLimits.Individual.SelectMany(x => x.Limits).Count());
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates!);
        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.AbstractionLimits.Aggregates[0].LinkedLicences![0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Aggregates.SelectMany(x => x.Limits).Count());
        
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal(LinkedLicenceSource.Document, agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].Source);
        
        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        Assert.Equal(LinkedLicenceSource.Document, agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].Source);
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);        
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);        
        
        // For second licence
        agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[1];
        
        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(LicenceStatus.Ok, agreedSchemaLicence.Status);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual!);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual!);
        Assert.Equal(4, agreedSchemaLicence.AbstractionLimits.Individual!.SelectMany(x => x.Limits).Count());
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates); // TODO take into account stuff in 9.2

        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(LinkedLicenceDirection.Outgoing, agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal(LinkedLicenceSource.Document, agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].Source);
        Assert.Equal(LinkedLicenceDirection.Incoming, agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("ReadInConjunction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal(LinkedLicenceSource.OtherDocument, agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].Source);
        Assert.Equal(LinkedLicenceDirection.Incoming, agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].Direction);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].LinkReason);
        Assert.Equal(LinkedLicenceSource.OtherDocument, agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].Source);
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        Assert.Equal("NE/026/0034/056", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[2].ContainedIn!);
        Assert.Equal(LinkedLicenceDirection.Incoming, agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].Direction);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition"
            , agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
        
        // For third licence
        agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[2];
        
        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(LicenceStatus.Ok, agreedSchemaLicence.Status);
        Assert.Equal(5, agreedSchemaLicence.AbstractionLimits.Individual!.Length);
        Assert.Equal(19, agreedSchemaLicence.AbstractionLimits.Individual!.SelectMany(x => x.Limits).Count());
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Aggregates!.Length);
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Aggregates.SelectMany(x => x.Limits).Count());
        
        Assert.Equal(4, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/027/0028/059", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("AggregateConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("AggregateConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        
        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[2].ContainedIn!.Length);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("ReadInConjunction", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
        Assert.Equal(LinkedLicenceSource.Document, agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].Source);
        
        Assert.Equal("NE/026/0034/056", agreedSchemaLicence.LinkedLicences[3].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[3].ContainedIn!);
        Assert.Equal(LinkedLicenceDirection.Incoming, agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].Direction);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].SectionName);
        Assert.Equal(LinkedLicenceSource.OtherDocument, agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].Source);
        Assert.Equal("SimultaneousDischargeCondition",
            agreedSchemaLicence.LinkedLicences[3].ContainedIn![0].LinkReason);   
        
        // For fourth licence
        agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[3];
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(LicenceStatus.NotFound, agreedSchemaLicence.Status);
        Assert.Empty(agreedSchemaLicence.AbstractionLimits.Individual!);
    }
    
    [Fact]
    public async Task When_GettingRecords_ShouldFindOne()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
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
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, 2, TestConfig.PdfFolder2))).Last();
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, 1, TestConfig.PdfFolder2));
        
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
        Assert.Equal(LicenceStatus.Ok, agreedSchemaLicence.Status);
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual!);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual!);
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);
        
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(LinkedLicenceDirection.Outgoing, agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal(LinkedLicenceDirection.Incoming, agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("ReadInConjunction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
        Assert.Equal(LinkedLicenceDirection.Incoming, agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].Direction);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].LinkReason);
        
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
        Assert.Equal(LicenceStatus.Ok, agreedSchemaLicence.Status);
        Assert.Equal(5, agreedSchemaLicence.AbstractionLimits.Individual!.Length);
        Assert.Equal(19, agreedSchemaLicence.AbstractionLimits.Individual!.SelectMany(x => x.Limits).Count());
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Aggregates!.Length);
        
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("NE/027/0028/059", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal(LinkedLicenceSource.Document, agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].Source);
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("AggregateConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
        Assert.Equal(LinkedLicenceSource.Document, agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].Source);
        
        Assert.Equal("NE/026/0034/018", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences[2].ContainedIn!.Length);
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].SectionName);
        Assert.Equal("ReadInConjunction", agreedSchemaLicence.LinkedLicences[2].ContainedIn![0].LinkReason);
        Assert.Equal("Purposes", agreedSchemaLicence.LinkedLicences[2].ContainedIn![1].SectionName);
        Assert.Equal("SubsequentAbstraction", agreedSchemaLicence.LinkedLicences[2].ContainedIn![1].LinkReason);
        Assert.Equal(LinkedLicenceDirection.Incoming, agreedSchemaLicence.LinkedLicences[2].ContainedIn![2].Direction);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[2].ContainedIn![2].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[2].ContainedIn![2].LinkReason);
        
        // For third licence
        agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[2];
        
        Assert.Equal(2, agreedSchemaLicence.LicenceSets.Length);
        Assert.Equal("NE0260034018-LV2019121120250331", agreedSchemaLicence.LicenceSets[0].LicenceSetId);
        Assert.Equal(expectedLicenceSetId, agreedSchemaLicence.LicenceSets[1].LicenceSetId);
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(LicenceStatus.NotFound, agreedSchemaLicence.Status);
    }
    
    [Fact]
    public async Task When_XBackLinkX_ThenNowGetsThem()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "NE0270023043__Application New Licence Issued 18.12.2018 10623801.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(13, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 2, TestConfig.PdfFolder2));
        
        Assert.Single(licenceSets);
        
        Assert.Equal("NE0270023043-LV2018121720290331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

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
        await SetupLicenceNumbersAsync(3);
        const string filename = "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 2, TestConfig.PdfFolder2));
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("NE0260034018-LV2019121120250331", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("1 April", agreedSchemaLicence.DefinitionOfYear!.StartDate);
        Assert.Equal("31 March", agreedSchemaLicence.DefinitionOfYear.EndDate);
        
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length);

        Assert.Equal("NE/026/0034/052", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        
        Assert.Equal("NE/026/0034/053", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
        Assert.Equal("SimultaneousDischargeCondition", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task When_FindingAdditionalInformationExtraReason()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12100074R01__Application WR Abstraction Licence Issued 11042025.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 3, 3);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].SectionName);
        Assert.Equal("UsedInConjunction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![2].LinkReason);        
    }
    
    [Fact]
    public async Task When_FindingAdditionalInformationExtraReason2()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "12100073R01__Application - New -  Issued Licence 31.03.2015 8814302.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("UsedInConjunction", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
    }
    
    [Fact]
    public async Task When_FindingFutherConditionsExtraReason()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "12100068__Application Normal Variation Licence Issued 17062025.docx.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "22631079__Application – Transfer – Issued Licence – 240223.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(13, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "12304073__Application –  New – Issued licence – November  2015 9083023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "12504142__Application Minor Variation Issued Licence - 27052025.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(13, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "12504008__Application - Minor Variation - Issued Licence PDF Copy 9211405.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(13, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "22702039__Application Formal Variation Issue Licence 30062023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        Assert.Equal("AdditionalInformation", agreedSchemaLicence.LinkedLicences[17].ContainedIn![0].SectionName);
        Assert.Equal("DonorLicence", agreedSchemaLicence.LinkedLicences[17].ContainedIn![0].LinkReason);
    }

    [Fact]
    public async Task WhenZaa_A()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "22727153__Application Formal Variation Issued Licence - 14122023.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 5);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "22722265__Application - new - issue licence 9393610.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "NE0270024056__Application Formal Variation Issued Licence - [11072017] - (11072017).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "22708052__Application - Formal Variation - Issued Licence 24.01.2017 9644004.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "22728270R01__Application - New - Issued Licence 24.06.2015 8918352.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "ne0230003031__Application – NA New – Issued Licence-22072022.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "NE0210000014__Application NA New Issued Licence 31-03-2021 11765884.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
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
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "NE0240005016__Application - Formal Variation -Application New Licence Issued 24_03_2021 00_00_00 11751498.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(13, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "NE0230001007__Application – NA New – Issued Licence-22072022.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "22722562R01__Application - Minor Variation -Application New Licence Issued 25_06_2019 00_00_00 10900765.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(16, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "NE0240005010__Application - New HEP Licence - Issued Licence 6 June 2013 7844848.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "22724199__Drax licence document - Amended 6065605.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(14, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "NE0270007008__Application New Issued Licence 31.03.2014 8288333.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
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
        await SetupLicenceNumbersAsync(3);
        const string filename = "22718077__Application - Minor Variation - Issued Licence 25.10.2016 9535704.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _naldLicenceStatusData,
            NaldData,
            _pdfDataExtractor,
            -1,
            await LookupConfigurationAsync(3, 3, TestConfig.PdfFolder3));
        
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal("22718077-LV20161012", licenceSets[0].LicenceSetId);
        Assert.Equal([LicenceSetType.SingleLicenceOnly], licenceSets[0].LicenceSetTypes);

        var agreedSchemaLicenceGroup = licenceSets[0];
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences[0];

        Assert.Equal("2/27/18/077", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]?.ToString());
        
        Assert.NotNull(agreedSchemaLicence.DefinitionOfYear);
        Assert.Equal(6, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual);
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits.Count);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates);
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits.Count);
        Assert.Equal(7, agreedSchemaLicence.AbstractionLimits.Aggregates[0].LinkedLicences?.Length); // TODO should be 6 - probably 7 as its referencing itself
    }
}