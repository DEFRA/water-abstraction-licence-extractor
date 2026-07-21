using FakeItEasy;
using Meziantou.Xunit;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tests.Helper;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[EnableParallelization]
[Collection("First Names 3")]
public class AzureAiVisionOcrPdfTests(SingletonFirstNamesFixture firstNamesFixture)
{
    private static readonly ICacheService CacheService;

    static AzureAiVisionOcrPdfTests()
    {
        var realCacheService = new FileSystemCacheService("Cache/");
        CacheService = GeneralTestsHelper.GetFakeCacheService(realCacheService, _naldData, _fileLicenceMapping);
    }
    
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

    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = A.Fake<IMessageQueueService>(); 
    
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
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
    
    private static readonly Dictionary<string, DmsFileData> _fileLicenceMapping = new() {{"", new DmsFileData()}};
    private readonly NaldLicenceStatusData _naldLicenceStatusData = new()
    {
        LiveLicences = [],
        LapsedLicences = [],
        ExpiredLicences = [],
        RevokedLicences = [],
        ImpoundmentLicences = []
    };
    private static readonly Dictionary<string, List<NaldData>> _naldData = [];

    private async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode, string pdfFolder)
    {
        return new LookupConfiguration(
            WalLabelConfiguration.GetLabels(),
            await firstNamesFixture.FirstNamesCsvTask(),
            new LocalFileService(pdfFolder),
            CacheService,
            regionCode,
            DateTime.Now);
    }

    private async Task<MatchesResult> GetMatchesAsync(string fileName, int regionCode, int number = 1)
    {
        var pdfFolder = number == 1 ? TestConfig.PdfFolder : TestConfig.PdfFolder2;
        if (number == 3) pdfFolder = TestConfig.PdfFolder3;
        else if (number == 5) pdfFolder = TestConfig.PdfFolder5;
        
        return (await _pdfDataExtractor.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            await LookupConfigurationAsync(regionCode, pdfFolder),
            [fileName],
            0)).Item!;
    }
    
    [Fact]
    public async Task FROM_6000_SET_LabelOver2Lines()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "22631093__Application - Issued Licence [23-10-1978] 6075944.pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 3);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var period = resultList
            .FirstOrDefault(result => result.LabelGroupName == "PeriodsOfAbstraction");
        
        Assert.NotNull(period);
        Assert.Equal("April to September", period.Text!.First().Text);

        var periods1 = period.SubResults[0];
        Assert.Equal("April", periods1.Text!.First().Text);

        var periods2 = period.SubResults[1];
        Assert.Equal("September", periods2.Text!.First().Text);
        
        var purpose = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");
        Assert.NotNull(purpose);
        Assert.Equal("Spray irrigation", purpose.Text!.First().Text);
        
        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var abstractionLimitsResult = resultList
            .FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(13, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);
        
        abstractionLimitsSection   = abstractionLimitsSection.SubResults[0];
        Assert.Equal(12, abstractionLimitsSection.SubResults.Count);
        
        var perDayUnitsAll = abstractionLimitsSection.SubResults
            .Where(x => x.MatchedLabel!.Name == "PerDayUnits")
            .ToList();

        Assert.Equal(4, perDayUnitsAll.Count);
        
        var perDayValuesAll = abstractionLimitsSection.SubResults
            .Where(x => x.MatchedLabel!.Name == "PerDayValue")
            .ToList();

        Assert.Equal(4, perDayValuesAll.Count);
        
        var perHourUnitsAll = abstractionLimitsSection.SubResults
            .Where(x => x.MatchedLabel!.Name == "PerHourUnits")
            .ToList();

        Assert.Equal(2, perHourUnitsAll.Count);
        
        var perHourValuesAll = abstractionLimitsSection.SubResults
            .Where(x => x.MatchedLabel!.Name == "PerHourValue")
            .ToList();

        Assert.Equal(2, perHourValuesAll.Count);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, TestConfig.PdfFolder3));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Individual!);
        Assert.Equal(6, agreedSchemaLicence.AbstractionLimits.Individual![0].Limits.Count);
        
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[0].Units);
        Assert.Equal(48, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerHour, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[0].PeriodType);
        
        Assert.Equal("gallons", agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[1].Units);
        Assert.Equal(10600, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[1].Value);
        Assert.Equal(LimitPeriodType.PerHour, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[1].PeriodType);
        
        Assert.Equal("thousand cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[2].Units);
        Assert.Equal(1, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[2].Value);
        Assert.Equal(LimitPeriodType.PerDay, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[2].PeriodType);
        
        Assert.Equal("gallons", agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[3].Units);
        Assert.Equal(220000, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[3].Value);
        Assert.Equal(LimitPeriodType.PerDay, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[3].PeriodType);
        
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[4].Units);
        Assert.Equal(384, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[4].Value);
        Assert.Equal(LimitPeriodType.PerDay, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[4].PeriodType);
        
        Assert.Equal("gallons", agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[5].Units);
        Assert.Equal(84500, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[5].Value);
        Assert.Equal(LimitPeriodType.PerDay, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[5].PeriodType);
        
        Assert.Single(agreedSchemaLicence.PeriodsOfAbstraction);
        Assert.Equal("April", agreedSchemaLicence.PeriodsOfAbstraction[0].StartDate);
        Assert.Equal("September", agreedSchemaLicence.PeriodsOfAbstraction[0].EndDate);
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task FROM_6000_SET_PurposeWasntSplitCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "22631097__Non-Application Licence Document (09.03.1988).pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(5, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var purpose = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");
        Assert.NotNull(purpose);
        Assert.Equal(2, purpose.Text!.Count);
        Assert.Equal("Spray Irrigation", purpose.Text!.First().Text);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, TestConfig.PdfFolder))).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task FROM_6000_SET_PurposeWasntSplitCorrectly2()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "22632235__Application Renewal - Licence Issued - 11112024.pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(15, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var purpose = resultList.FirstOrDefault(result => result.LabelGroupName == "Purposes");
        Assert.NotNull(purpose);
        Assert.Equal(3, purpose.Text!.Count);
        Assert.Equal("4.1 Spray irrigation.", purpose.Text[1].Text);
        
        var additionalInformation = resultList.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.NotNull(additionalInformation);
        Assert.Equal(19, additionalInformation.Text!.Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(12, records.Text!.Count);
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, TestConfig.PdfFolder))).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task Handsigned_WhenNearPreviousLineIsCompany_ThenFoundCorrect_Ish()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Non-Application Licence Document (22.09.1986).PDF";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(8, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("SOUTHERN WATER AUTHORITY", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("22ND DAY OF SEPTEMBER 1986", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        // NOTE - According to companies house this is actual H.N. BUTLER FARMS LTD        
        Assert.Equal("H. W. Butter Farms Ltd", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("( hereinafter referred to as \"The Licence Holder\" )", nameResult.MatchedLabel!.Text!.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.OnOrNearPreviousLine, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(3, abstractionLimitsResult.Text?.Count);

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
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder))).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("11/42/28.2/49", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
    }
    
    [Fact]
    public async Task VeryFaintText_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Old 6078942.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(8, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("MERSEY AND WEAVER RIVER Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("twenty-third day of March, 19 66", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("MID CHESHIRE WATER BOARD", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResult.MatchedLabel!.Text?.Select(x => x.Text)!, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.FullyOnSameLine, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);
        
        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        Assert.Equal(12, section1Sub1.SubResults!.Count);
        
        // This file incorrectly gets results that have been crossed out
        var perYearUnits1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("million gallons", perYearUnits1?.Text?.FirstOrDefault()?.Text);

        var perYearValue1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("1.095", perYearValue1?.Text?.FirstOrDefault()?.Text); // TODO Should actually be 1,095
        
        var perYearUnits2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("megalitres", perYearUnits2?.Text?.FirstOrDefault()?.Text);

        var perYearValue2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("720", perYearValue2?.Text?.FirstOrDefault()?.Text); // TODO Should actually be 3273.2
        
        var perDayUnits1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("million gallons", perDayUnits1?.Text?.FirstOrDefault()?.Text);

        var perDayValue1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("3.5", perDayValue1?.Text?.FirstOrDefault()?.Text);

        var perDayUnits2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("megalitres", perDayUnits2?.Text?.FirstOrDefault()?.Text);

        var perDayValue2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("2.25", perDayValue2?.Text?.FirstOrDefault()?.Text); // TODO should be 10.2
        
        var perHourUnits1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("thousand gallons", perHourUnits1?.Text?.FirstOrDefault()?.Text);

        var perHourValue1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("210", perHourValue1?.Text?.FirstOrDefault()?.Text);
        
        var perHourUnits2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("cubic metres", perHourUnits2?.Text?.FirstOrDefault()?.Text);

        var perHourValue2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("95", perHourValue2?.Text?.FirstOrDefault()?.Text); // TODO should be 431
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/68/1/158", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder))).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.NotEmpty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task X_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Issued Licence - 01081966.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("THE SOMERSET RIVER AUTHORITY", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("First", dateOfIssue.Text?.FirstOrDefault()?.Text); // TODO wrong
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("SHERBORNE SCHOOL", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("authority hereby licence", nameResult.MatchedLabel!.Text?.Select(x => x.Text)!, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResult.MatchedPosition);
        
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
        var section1Sub1 = abstractionLimitsSection.SubResults[0];
        Assert.Equal(16, section1Sub1.SubResults.Count);
        
        Assert.Equal(2, section1Sub1.SubResults.Count(x => x.MatchedLabel!.Name == "PerHourUnits"));
        Assert.Equal(2, section1Sub1.SubResults.Count(x => x.MatchedLabel!.Name == "PerHourValue"));
        Assert.Equal(2, section1Sub1.SubResults.Count(x => x.MatchedLabel!.Name == "PerDayUnits"));
        Assert.Equal(2, section1Sub1.SubResults.Count(x => x.MatchedLabel!.Name == "PerDayValue"));
        Assert.Equal(2, section1Sub1.SubResults.Count(x => x.MatchedLabel!.Name == "PerMonthUnits"));
        Assert.Equal(2, section1Sub1.SubResults.Count(x => x.MatchedLabel!.Name == "PerMonthValue"));
        Assert.Equal(2, section1Sub1.SubResults.Count(x => x.MatchedLabel!.Name == "PerYearUnits"));
        Assert.Equal(2, section1Sub1.SubResults.Count(x => x.MatchedLabel!.Name == "PerYearValue"));
        
        var perHourUnits = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1200", perHourValue?.Text?.FirstOrDefault()?.Text);

        var perDayUnits1 = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits1?.Text?.FirstOrDefault()?.Text);

        var perDayValue1 = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("13400", perDayValue1?.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits2 = section1Sub1.SubResults?
            .LastOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits2?.Text?.FirstOrDefault()?.Text);

        var perDayValue2 = section1Sub1.SubResults?
            .LastOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("26700", perDayValue2?.Text?.FirstOrDefault()?.Text);

        var perMonthUnits1 = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerMonthUnits");
        Assert.Equal("gallons", perMonthUnits1?.Text?.FirstOrDefault()?.Text);

        var perMonthValue1 = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerMonthValue");
        Assert.Equal("134000", perMonthValue1?.Text?.FirstOrDefault()?.Text);

        var perMonthUnits2 = section1Sub1.SubResults?
            .LastOrDefault(x => x.MatchedLabel!.Name == "PerMonthUnits");
        Assert.Equal("gallons", perMonthUnits2?.Text?.FirstOrDefault()?.Text);

        var perMonthValue2 = section1Sub1.SubResults?
            .LastOrDefault(x => x.MatchedLabel!.Name == "PerMonthValue");
        Assert.Equal("267000", perMonthValue2?.Text?.FirstOrDefault()?.Text);        
        
        var perYearUnits = section1Sub1.SubResults?
            .FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("gallons", perYearUnits?.Text?.FirstOrDefault()?.Text);

        var perYearValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("667000", perYearValue?.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("16/52/02/G/037", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder))).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact(Skip = "TEST BROKEN WITH NEW IMPLEMENTATION ")]
    public async Task Succession_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Non-Application Licence Document (08.06.1987).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResult);
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("H.H. Henderson & C. Wentworth-Stanley", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["Succession to licence", "as amended by"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.OnOrNearPreviousLine, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        
        Assert.Equal(2, section1Sub1.SubResults.Count);
        
        var perHourUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("gallons", perHourUnits?.Text?.FirstOrDefault()?.Text);

        var perHourValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("1500", perHourValue?.Text?.FirstOrDefault()?.Text); // TODO maybe should be 5183?
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("gallons", perDayUnits?.Text?.FirstOrDefault()?.Text);

        // Surprisingly the OCR really struggles with this document (TODO fix for this)
        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("5183", perDayValue?.Text?.FirstOrDefault()?.Text); // Should actually be 5600    
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/271", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
        
        var agreedSchemaLicenceGroup = (await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder))).Last();

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task WhenZ_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "6.5.4_Application_New_Issued_Licence_20.08.2014.pdf";
        
        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        
        var dateOfIssue = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(new DateTime(2014, 08, 20), agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task WhenNearPreviousLineIsCompany_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "14460030853 licence effective 24.07.2005.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var dateOfIssue = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        
        var dateOfOriginalIssue = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateOfOriginalIssue");
        Assert.NotNull(dateOfOriginalIssue);
        
        var dateEffective = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateEffective");
        Assert.NotNull(dateEffective);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr T M C Davey", nameResult.Text?[0]?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("14/46/03/0853", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        Assert.Equal(8, section1Sub1.SubResults!.Count);

        var linkedLicences = section1Sub1.SubResults.Where(x => x.MatchedLabel?.Name == "LinkedLicenceNumber").ToList();
        Assert.Single(linkedLicences);
        Assert.Equal("14/46/03/0852", linkedLicences[0].Text!.First().Text);
        
        var linkedLicenceFilenames = section1Sub1.SubResults.Where(x => x.MatchedLabel?.Name == "LinkedLicenceFilename");
        Assert.Empty(linkedLicenceFilenames);
        
        Assert.Equal("1 January and ending on 31 December", section1Sub1.SubResults.Last().Text!.Single().Text);        
        
        var perDayUnits = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("cubic metres", perDayUnits?.Text?.FirstOrDefault()?.Text);

        var perDayValue = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("77", perDayValue?.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("cubic metres", perYearUnits1?.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits2 = section1Sub1.SubResults!.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("cubic metres", perYearUnits2?.Text?.FirstOrDefault()?.Text);

        var perYearValue1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("5116", perYearValue1?.Text?.FirstOrDefault()?.Text); // This is actually from 1 april to 30 sept per year
 
        var perYearValue2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("5116", perYearValue2!.Text?.FirstOrDefault()?.Text); // This is actually from 1 april to 30 sept per year
        
        // TODO - other 2 things
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("14/46/03/0852", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);        
    }
    
    [Fact]
    public async Task WhenIsOldCrossedOut_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Old 6082700.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(9, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("MERSEY AND WEAVER RIVER AUTHORITY", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("third day of April 19 70", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResult);
        // Is crossed out but Azure AI can read it
        Assert.Equal("WARRINGTON, RUNCORN AND DISTRICT WATER BOARD", nameResult.Text?.First().Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult); // Is crossed out but Azure AI can read it
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);
        
        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults![0];
        Assert.Equal(13, section1Sub1.SubResults!.Count);
        
        var pointName = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition")?.Text!.First().Text;
        
        Assert.Equal("(1)", pointName);
        
        var perDayUnits1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("million gallons", perDayUnits1?.Text?.FirstOrDefault()?.Text);

        var perDayValue1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("1.25", perDayValue1?.Text?.FirstOrDefault()?.Text);    
        
        var perDayUnits2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayUnits");
        Assert.Equal("megalitres", perDayUnits2?.Text?.FirstOrDefault()?.Text);

        var perDayValue2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerDayValue");
        Assert.Equal("1.25", perDayValue2?.Text?.FirstOrDefault()?.Text); // TODO should be 5.7
        
        var perYearUnits1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("million gallons", perYearUnits1?.Text?.FirstOrDefault()?.Text);

        var perYearValue1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("300", perYearValue1?.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits2 = section1Sub1.SubResults!.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearUnits");
        Assert.Equal("megalitres", perYearUnits2?.Text?.FirstOrDefault()?.Text);

        var perYearValue2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerYearValue");
        Assert.Equal("300", perYearValue2?.Text?.FirstOrDefault()?.Text); // TODO should be 1364
        
        var perHourUnits1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("thousand gallons", perHourUnits1?.Text?.FirstOrDefault()?.Text);

        var perHourValue1 = section1Sub1.SubResults?.FirstOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("52", perHourValue1?.Text?.FirstOrDefault()?.Text);
        
        var perHourUnits2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerHourUnits");
        Assert.Equal("cubic metres", perHourUnits2?.Text?.FirstOrDefault()?.Text);

        var perHourValue2 = section1Sub1.SubResults?.LastOrDefault(x => x.MatchedLabel!.Name == "PerHourValue");
        Assert.Equal("52", perHourValue2?.Text?.FirstOrDefault()?.Text); // TODO should be 236
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/68/3/91", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        // TODO - other 2 things
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(3, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("25/69/3/91", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("25/68/3/76", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal("25/68/5/9", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
    }
    
    [Fact]
    public async Task Z1_X2_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "14460030852 licence effective 24.07.2005.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr T M C Davey", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.True(abstractionLimitsSection.IsOcr);
        Assert.Equal(10, abstractionLimitsSection.Text?.Count);
        
        Assert.Single(abstractionLimitsSection.SubResults!);
        Assert.Equal(10, abstractionLimitsSection.SubResults![0].Text!.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, licenceNumberResult.MatchedLabel!.Position);        
        Assert.Equal("14/46/03/0852", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task Z2_X3_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "1-21-00-010 5822315.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("28 DAY OF March 1984", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var issuer = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuer);
        Assert.StartsWith("NORTHUMBRIAN WATER AUTHORITY", issuer.Text?.FirstOrDefault()?.Text);
        
        // Assert
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("A A C McArthur", nameResult.Text?.FirstOrDefault()?.Text); // TODO should be just A A C McArthur
        Assert.Equal(["Licensee"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.FullyOnSameLine, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsSection);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, licenceNumberResult.MatchedLabel!.Position);        
        Assert.Equal("1/21/0/10", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task Z3_X3_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "08-36-19-S-0101 5826949.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var pointResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        
        Assert.NotNull(pointResult);
        Assert.True(pointResult.IsOcr);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);
        
        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("8/36/19/S/101", agreedSchemaLicence.NoneSchemaData["scrapedLicenceNumber"]);
        Assert.Equal("08/36/19/S/0101", agreedSchemaLicence.LicenceNumber?.Value);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("8/36/19/S/130", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }
    
    [Fact(Skip = "DebuggingImageIssue")]
    public async Task ScannedFileUploaded_ThenFindXuncorn_DebuggingTest()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Old 6082700.PDF";

        if (File.Exists("Licence - Old 6082700/PdfPig/Text/cache-metadata.json"))
        {
            File.Delete("Licence - Old 6082700/PdfPig/Text/cache-metadata.json");
        }        
        
        if (File.Exists("Licence - Old 6082700/PdfPig/Images/cache-metadata.json"))
        {
            File.Delete("Licence - Old 6082700/PdfPig/Images/cache-metadata.json");
        }

        if (File.Exists("Licence - Old 6082700/PdfPig/Images/page-1-image-1.bmp"))
        {
            File.Delete("Licence - Old 6082700/PdfPig/Images/page-1-image-1.bmp");
        }

        if (File.Exists("Licence - Old 6082700/AzureAiVisionOcr/Text/ocr-page-1-image-1.json"))
        {
            File.Delete("Licence - Old 6082700/AzureAiVisionOcr/Text/ocr-page-1-image-1.json");
        }

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);

        // Assert
        var allText = string.Join(' ', resultFull.Pages[0].Providers[1].Text!);
        Assert.Contains("UNCORN", allText);
    }
    
    [Fact]
    public async Task When_YorkshireWaterCompany1_ThenY()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "2-26-32-126 6937559.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        
        var companyName = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.StartsWith("YORKSHIRE W", companyName?.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        
        var dateOfOriginalIssue = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateOfOriginalIssue");
        Assert.NotNull(dateOfOriginalIssue);
        
        var dateEffective = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "DateEffective");
        Assert.NotNull(dateEffective);
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        Assert.Equal(3, points.Text!.Count);
        Assert.StartsWith("At National Grid Reference", points.Text![0].Text);
        Assert.Equal("and \"F\" on the map.", points.Text![2].Text);

        var pointPurposeGroup = points.SubResults
            .Where(psr => psr.MatchedLabel?.Name == "PointPurposeGroup")
            .ToList();

        Assert.Single(pointPurposeGroup);
        
        var pointsSubs = pointPurposeGroup[0].SubResults
            .Where(psr => psr.MatchedLabel?.Name == "Point")
            .ToList();
        
        Assert.Equal(4, pointsSubs.Count);
        
        Assert.Equal("(1) TA 0417 2942", pointsSubs[0].Text!.FirstOrDefault()!.Text);
        Assert.Equal("(2) TA 0472 3425", pointsSubs[1].Text!.FirstOrDefault()!.Text);
        Assert.Equal(2, pointsSubs[2].Text!.Count);
        Assert.Equal("(3) TA 0677 3514 &", pointsSubs[2].Text!.FirstOrDefault()!.Text);
        Assert.Equal("TA 0678 3508 &", pointsSubs[2].Text!.LastOrDefault()!.Text);
        Assert.Equal("(4) TA 0269 3303 & TA 0268 3302 marked \"A\", \"B\", \"C\", \"D\", \"E\"", pointsSubs[3].Text!.FirstOrDefault()!.Text);
        
        var purpose = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Purposes");
        Assert.NotNull(purpose);
        
        Assert.Equal(2, purpose.Text!.Count);
        Assert.Equal("4. PURPOSE OF ABSTRACTION", purpose.Text![0].Text);
        Assert.Equal("Water undertaking.", purpose.Text![1].Text);
        
        var abstractionLimitsResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(15, abstractionLimitsResult.Text?.Count);
        Assert.Equal("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE", abstractionLimitsResult.Text![0].Text);

        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Equal(5, abstractionLimitsSections.Count);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults[0];
        
        var pointName = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel?.Name == "PointCondition")?.Text!.First().Text;
        
        Assert.Equal("(1)", pointName);
        
        Assert.Equal(3, section1Sub1.Text?.Count);
        Assert.Equal(5, section1Sub1.SubResults.Count);

        var units1 = section1Sub1.SubResults[1];
        Assert.Equal("cubic metres", units1.Text![0].Text);
        Assert.Equal("PerDayUnits", units1.MatchedLabel!.Name);
        Assert.Equal(31, units1.LineNumber);
        
        var units2 = section1Sub1.SubResults[2];
        Assert.Equal("cubic metres", units2.Text![0].Text);
        Assert.Equal("PerYearUnits", units2.MatchedLabel!.Name);
        Assert.Equal(32, units2.LineNumber);
        
        var value1 = section1Sub1.SubResults[3];
        Assert.Equal("45460.92", value1.Text![0].Text);
        Assert.Equal("PerDayValue", value1.MatchedLabel!.Name);
        
        var value2 = section1Sub1.SubResults[4];
        Assert.Equal("13638276", value2.Text![0].Text);
        Assert.Equal("PerYearValue", value2.MatchedLabel!.Name);
        
        abstractionLimitsSection = abstractionLimitsSections[4];
        
        var section5Sub1 = abstractionLimitsSection.SubResults[0];
        Assert.Equal(4, section5Sub1.SubResults.Count);
        
        var units3 = section5Sub1.SubResults[0];
        Assert.Equal("cubic metres", units3.Text![0].Text);
        Assert.Equal("PerDayUnits", units3.MatchedLabel!.Name);
        Assert.Equal("Units", section5Sub1.SubResults[0].MatchedLabel?.Format);
        Assert.Equal(10, units3.LineNumber);
        
        var units4 = section5Sub1.SubResults[1];
        Assert.Equal("cubic metres", units4.Text![0].Text);
        Assert.Equal("PerYearUnits", units4.MatchedLabel!.Name);
        Assert.Equal(10, units4.LineNumber);
        
        var value3 = section5Sub1.SubResults[2];
        Assert.Equal("100000", value3.Text![0].Text);
        Assert.Equal("PerDayValue", value3.MatchedLabel!.Name);
        
        var value4 = section5Sub1.SubResults[3];
        Assert.Equal("32850000", value4.Text![0].Text);
        Assert.Equal("PerYearValue", value4.MatchedLabel!.Name);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(filename, agreedSchemaLicence.Filename);
        Assert.Equal("2/26/32/126", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(new DateTime(2005, 07, 20), agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Equal(new DateTime(1966, 01, 27), agreedSchemaLicence.LicenceVersion.OriginalIssueDate);
        Assert.Equal(new DateTime(2005, 02, 02), agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Null(agreedSchemaLicence.LicenceVersion.ExpiryDate);
        Assert.Equal("22632126-LV20050202", agreedSchemaLicence.Id);
        Assert.Equal("LV20050202", agreedSchemaLicence.LicenceVersion.LicenceVersionId);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates!);
        
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Aggregates![0].Limits.Count);
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits[0].Units);        
        Assert.Equal(100000, agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Equal("cubic metres", agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits[1].Units);        
        Assert.Equal(32850000, agreedSchemaLicence.AbstractionLimits.Aggregates[0].Limits[1].Value);
        
        Assert.Equal(4, agreedSchemaLicence.AbstractionLimits.Individual!.Length);

        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual![0].Limits.Count);
        var limitGroup = agreedSchemaLicence.AbstractionLimits.Individual[0];
        
        Assert.Equal("cubic metres", limitGroup.Limits[0].Units);
        Assert.Equal(LimitPeriodType.PerDay, limitGroup.Limits[0].PeriodType);
        Assert.Equal(45460.92, limitGroup.Limits[0].Value);
        Assert.Single(limitGroup.Limits[0].Points!);
        Assert.Equal("(1)", limitGroup.Limits[0].Points![0].Id);
        
        Assert.Equal("cubic metres", limitGroup.Limits[1].Units);
        Assert.Equal(13638276, limitGroup.Limits[1].Value);
        Assert.Equal(LimitPeriodType.PerYear, limitGroup.Limits[1].PeriodType);
        Assert.Single(limitGroup.Limits[1].Points!);
        Assert.Equal("(1)", limitGroup.Limits[1].Points![0].Id);
        
        limitGroup = agreedSchemaLicence.AbstractionLimits.Individual[1];
        
        Assert.Equal("cubic metres", limitGroup.Limits[0].Units);
        Assert.Equal(68191, limitGroup.Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerDay, limitGroup.Limits[0].PeriodType);
        Assert.Single(limitGroup.Limits[0].Points!);
        Assert.Equal("(2)", limitGroup.Limits[0].Points![0].Id);
        
        Assert.Equal("cubic metres", limitGroup.Limits[1].Units);
        Assert.Equal(18184368, limitGroup.Limits[1].Value);
        Assert.Equal(LimitPeriodType.PerYear, limitGroup.Limits[1].PeriodType);
        Assert.Single(limitGroup.Limits[1].Points!);
        Assert.Equal("(2)", limitGroup.Limits[1].Points![0].Id);
        
        limitGroup = agreedSchemaLicence.AbstractionLimits.Individual[2];
        
        Assert.Equal("cubic metres", limitGroup.Limits[0].Units);
        Assert.Equal(45461, limitGroup.Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerDay, limitGroup.Limits[0].PeriodType);
        Assert.Single(limitGroup.Limits[0].Points!);
        Assert.Equal("(3)", limitGroup.Limits[0].Points![0].Id);
        
        Assert.Equal("cubic metres", limitGroup.Limits[1].Units);
        Assert.Equal(16593236, limitGroup.Limits[1].Value);
        Assert.Equal(LimitPeriodType.PerYear, limitGroup.Limits[1].PeriodType);
        Assert.Single(limitGroup.Limits[1].Points!);
        Assert.Equal("(3)", limitGroup.Limits[1].Points![0].Id);
        
        limitGroup = agreedSchemaLicence.AbstractionLimits.Individual[3];
        
        Assert.Equal("cubic metres", limitGroup.Limits[0].Units);
        Assert.Equal(15911, limitGroup.Limits[0].Value);
        Assert.Equal(LimitPeriodType.PerDay, limitGroup.Limits[0].PeriodType);
        Assert.Single(limitGroup.Limits[0].Points!);
        Assert.Equal("(4)", limitGroup.Limits[0].Points![0].Id);
        
        Assert.Equal("cubic metres", limitGroup.Limits[1].Units);
        Assert.Equal(5819000, limitGroup.Limits[1].Value);
        Assert.Equal(LimitPeriodType.PerYear, limitGroup.Limits[1].PeriodType);
        Assert.Single(limitGroup.Limits[1].Points!);
        Assert.Equal("(4)", limitGroup.Limits[1].Points![0].Id);
        
        Assert.Equal(4, agreedSchemaLicence.Points.Length);
        Assert.Equal("(1)", agreedSchemaLicence.Points[0].Id);
        Assert.Equal("TA 0417 2942", agreedSchemaLicence.Points[0].Description);
        Assert.Equal("(2)", agreedSchemaLicence.Points[1].Id);
        Assert.Equal("TA 0472 3425", agreedSchemaLicence.Points[1].Description);
        Assert.Equal("(3)", agreedSchemaLicence.Points[2].Id);
        Assert.Equal("TA 0677 3514 & TA 0678 3508 &", agreedSchemaLicence.Points[2].Description);
        Assert.Equal("(4)", agreedSchemaLicence.Points[3].Id);
        Assert.Equal("TA 0269 3303 & TA 0268 3302", agreedSchemaLicence.Points[3].Description);
        
        // TODO 1 for each point
        
        Assert.Single(agreedSchemaLicence.Purposes);
        // TODO filling in purpose
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_YorkshireWaterCompany2_ThenY()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "2-27-29-012 7003124.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        
        var licenceNumberResult = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("2/27/29/12", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        
        var company = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(company);
        Assert.Equal("SCARBOROUGH CORPORATION", company.Text?.Single().Text);
        
        var abstractionLimitsResult = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(5, abstractionLimitsResult.Text?.Count);
        
        var abstractionLimitsSections = abstractionLimitsResult.SubResults;
        Assert.NotNull(abstractionLimitsSections);
        Assert.Single(abstractionLimitsSections);

        var abstractionLimitsSection = abstractionLimitsSections[0];
        Assert.NotNull(abstractionLimitsSection);
        Assert.Equal(5, abstractionLimitsSection.Text!.Count);
        
        Assert.NotNull(abstractionLimitsSection.SubResults);

        Assert.Single(abstractionLimitsSection.SubResults);
        var section1Sub1 = abstractionLimitsSection.SubResults[0];
        
        Assert.Equal(14, section1Sub1.SubResults.Count); // 6 units, 6 values, 2 dates

        var datePeriod1 = section1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Name == "DatePurposeRough");
        Assert.Equal("November to May", datePeriod1?.Text?.FirstOrDefault()?.Text);

        var datePeriod2 = section1Sub1.SubResults
            .LastOrDefault(x => x.MatchedLabel!.Name == "DatePurposeRough");
        Assert.Equal("June to October", datePeriod2?.Text?.FirstOrDefault()?.Text);

        var perDayUnitsAll = section1Sub1.SubResults
            .Where(x => x.MatchedLabel!.Name == "PerDayUnits")
            .ToList();

        Assert.Equal(4, perDayUnitsAll.Count);
        
        var perDayUnits = perDayUnitsAll[0];
        Assert.Equal("thousand cubic metres", perDayUnits.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = perDayUnitsAll[1];
        Assert.Equal("million gallons", perDayUnits.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = perDayUnitsAll[2];
        Assert.Equal("thousand cubic metres", perDayUnits.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = perDayUnitsAll[3];
        Assert.Equal("million gallons", perDayUnits.Text?.FirstOrDefault()?.Text);

        var perDayValueAll = section1Sub1.SubResults
            .Where(x => x.MatchedLabel!.Name == "PerDayValue")
            .ToList();

        Assert.Equal(4, perDayValueAll.Count);
        
        var perDayValue = perDayValueAll[0];
        Assert.Equal("20.45", perDayValue.Text?.FirstOrDefault()?.Text);

        perDayValue = perDayValueAll[1];
        Assert.Equal("4.5", perDayValue.Text?.FirstOrDefault()?.Text);        
        
        perDayValue = perDayValueAll[2];
        Assert.Equal("22.73", perDayValue.Text?.FirstOrDefault()?.Text);
        
        perDayValue = perDayValueAll[3];
        Assert.Equal("5", perDayValue.Text?.FirstOrDefault()?.Text);

        var perYearUnitsAll = section1Sub1.SubResults
            .Where(x => x.MatchedLabel!.Name == "PerYearUnits")
            .ToList();
        
        Assert.Equal(2, perYearUnitsAll.Count);

        var perYearUnits = perYearUnitsAll[0];
        Assert.Equal("thousand cubic metres", perYearUnits.Text?.FirstOrDefault()?.Text);

        perYearUnits = perYearUnitsAll[1];
        Assert.Equal("million gallons", perYearUnits.Text?.FirstOrDefault()?.Text);

        var perYearValueAll = section1Sub1.SubResults
            .Where(x => x.MatchedLabel!.Name == "PerYearValue")
            .ToList();
        
        Assert.Equal(2, perYearValueAll.Count);
        
        var perYearValue = perYearValueAll[0];
        Assert.Equal("7823", perYearValue?.Text?.FirstOrDefault()?.Text);
        
        perYearValue = perYearValueAll[1];
        Assert.Equal("1721", perYearValue?.Text?.FirstOrDefault()?.Text);
        
        var points = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        Assert.Equal(2, points.Text!.Count); // TODO should be 5
        //Assert.Equal("Source of supply and authorised place(s) of abstraction", points.Text![0].Text);
        //Assert.StartsWith("Delete the existing", points.Text![1].Text);
        //Assert.Equal("the following :", points.Text![2].Text); // TODO work out what is hapening here
        Assert.Equal("NZ 886 088 River Esk at Ruswarp", points.Text![0].Text);
        Assert.Equal("NZ 873 082 River Esk at Briggswath", points.Text![1].Text);

        Assert.Equal(2, points.SubResults.Count);
        
        var pointPurposeGroup = points.SubResults.First();
        Assert.NotNull(pointPurposeGroup);

        var pointsSubs = pointPurposeGroup.SubResults;
        Assert.Single(pointsSubs);
        
        pointPurposeGroup = points.SubResults.Last();
        Assert.NotNull(pointPurposeGroup);

        pointsSubs = pointPurposeGroup.SubResults;
        
        var purpose = resultFull.Matches!.FirstOrDefault(result => result.LabelGroupName == "Purposes");
        Assert.NotNull(purpose);
        
        Assert.Equal(2, purpose.Text!.Count);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder));

        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("2/27/29/012", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal(filename, agreedSchemaLicence.Filename);
        
        Assert.NotNull(agreedSchemaLicence.AbstractionLimits);
        Assert.Null(agreedSchemaLicence.AbstractionLimits.Aggregates);
        
        Assert.Equal(3, agreedSchemaLicence.AbstractionLimits.Individual!.Length);
        
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits.Count);
        Assert.Equal(7823, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[0].Value);
        Assert.Equal("thousand cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[0].Units);
        Assert.Equal(1721, agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[1].Value);
        Assert.Equal("million gallons", agreedSchemaLicence.AbstractionLimits.Individual[0].Limits[1].Units);
        
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual[1].Limits.Count);
        Assert.Equal(20.45, agreedSchemaLicence.AbstractionLimits.Individual[1].Limits[0].Value);
        Assert.Equal("thousand cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[1].Limits[0].Units);
        Assert.Equal(4.5, agreedSchemaLicence.AbstractionLimits.Individual[1].Limits[1].Value);
        Assert.Equal("million gallons", agreedSchemaLicence.AbstractionLimits.Individual[1].Limits[1].Units);
        Assert.Equal("November", agreedSchemaLicence.AbstractionLimits.Individual[1].TimePeriod!.StartDate);
        Assert.Equal("May", agreedSchemaLicence.AbstractionLimits.Individual[1].TimePeriod!.EndDate);
        
        Assert.Equal(2, agreedSchemaLicence.AbstractionLimits.Individual[2].Limits.Count);
        Assert.Equal(22.73, agreedSchemaLicence.AbstractionLimits.Individual[2].Limits[0].Value);
        Assert.Equal("thousand cubic metres", agreedSchemaLicence.AbstractionLimits.Individual[2].Limits[0].Units);
        Assert.Equal(5, agreedSchemaLicence.AbstractionLimits.Individual[2].Limits[1].Value);
        Assert.Equal("million gallons", agreedSchemaLicence.AbstractionLimits.Individual[1].Limits[1].Units);

        Assert.Equal("June", agreedSchemaLicence.AbstractionLimits.Individual[2].TimePeriod!.StartDate);
        Assert.Equal("October", agreedSchemaLicence.AbstractionLimits.Individual[2].TimePeriod!.EndDate);
        
        Assert.Equal("SCARBOROUGH CORPORATION", agreedSchemaLicence.NoneSchemaData["issuedTo"]);
        Assert.Equal(new DateTime(1966, 01, 27), agreedSchemaLicence.LicenceVersion.IssueDate);
        Assert.Null(agreedSchemaLicence.LicenceVersion.OriginalIssueDate);
        Assert.Null(agreedSchemaLicence.LicenceVersion.EffectiveDate);
        Assert.Equal("22729012-LV19660127", agreedSchemaLicence.Id);
        Assert.Equal("LV19660127", agreedSchemaLicence.LicenceVersion.LicenceVersionId);

        Assert.Equal(2, agreedSchemaLicence.Points.Length);
        Assert.Single(agreedSchemaLicence.Purposes);
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_PurposeHasSubPointsInIt_ThenNowGetsThem()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "22713185__Non-Application Licence Documents (20.12.1996).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 2);
        Assert.Equal(10, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder2));
        
        Assert.Single(agreedSchemaLicenceGroup);
        var agreedSchemaLicence = agreedSchemaLicenceGroup.First().Licences.Single();
        
        Assert.Equal(2, agreedSchemaLicence.Purposes.Length);
        Assert.Equal("Through flow for Pugneys Country Park Lake", agreedSchemaLicence.Purposes[0].Description);
        Assert.Equal("Augmentation of Pugneys Country Park Lake for subsequent bowser abstraction", agreedSchemaLicence.Purposes[1].Description);
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_PurposeHasSubPointsInIt33_ThenNowGetsThem()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "2671309044__Application type unknown Licence Issued (30102002).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 2);
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(3, TestConfig.PdfFolder2));
        
        Assert.Single(agreedSchemaLicenceGroup);
        var agreedSchemaLicence = agreedSchemaLicenceGroup.First().Licences.Single();
        
        Assert.Single(agreedSchemaLicence.Purposes);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_PurposeHasSubPointsInIt44_ThenNowGetsThem()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "2671311013__Non-Application Licence Document (09.01.1985).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 2);
        Assert.Equal(5, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(1, TestConfig.PdfFolder2));
        
        Assert.Single(agreedSchemaLicenceGroup);
        var agreedSchemaLicence = agreedSchemaLicenceGroup.First().Licences.Single();
        
        Assert.Equal(2, agreedSchemaLicence.Purposes.Length);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task When_LinkedLicenceLooksSuspect_ThenNowGetsThem()
    {
        // Arrange
        await SetupLicenceNumbersAsync(2);
        const string filename = "22720211__Non-Application Licence Document (01.12.1990).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 2, 2);
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);

        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(2, TestConfig.PdfFolder2));
        
        Assert.Single(agreedSchemaLicenceGroup);
        var agreedSchemaLicence = agreedSchemaLicenceGroup.First().Licences.First();

        Assert.Equal("2/27/20/211", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task When_LicenceNumberWithMissingSpaceInFront()
    {
        // Arrange
        await SetupLicenceNumbersAsync(3);
        const string filename = "22712254__2-27-12-254 6960530.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 3, 5);
        Assert.Equal(8, GeneralTestsHelper.ExcludeSomeMatches(resultFull.Matches!).Count);

        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _pdfDataExtractor,
            0,
            await LookupConfigurationAsync(2, TestConfig.PdfFolder));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        var agreedSchemaLicence = agreedSchemaLicenceGroup.First().Licences.First();

        Assert.Equal("2/27/12/254", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.NotEmpty(agreedSchemaLicence.LinkedLicences);
        Assert.Single(agreedSchemaLicence.AbstractionLimits.Aggregates![0].LinkedLicences!);
    }
}