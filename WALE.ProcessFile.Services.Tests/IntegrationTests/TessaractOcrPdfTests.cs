using Meziantou.Xunit;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Tesseract;
using WALE.ProcessFile.Services.Tests.Helper;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[EnableParallelization]
[Collection("First Names 2")]
public class TessaractOcrPdfTests(SingletonFirstNamesFixture firstNamesFixture)
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
    
    private readonly IPdfDataExtractorService _pdfDataExtractorCombined1 = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(
                TestConfig.TesseractPath,
                Core.Enums.PageSegMode.SparseTextOsd,
                CacheService, OutputService,
                TestConfig.DotnetPath,
                TestConfig.TesseractExeName,
                TestConfig.TesseractExeDirectory),
            new TesseractOcrDataExtractorService(
                TestConfig.TesseractPath,
                Core.Enums.PageSegMode.Auto,
                CacheService,
                OutputService,
                TestConfig.DotnetPath,
                TestConfig.TesseractExeName,
                TestConfig.TesseractExeDirectory),
        },
        CacheService,
        OutputService,
        DocumentService,
        TestConfig.PdfFolder);
    
    private readonly IPdfDataExtractorService _pdfDataExtractorCombined3 = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, Core.Enums.PageSegMode.SparseTextOsd, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, Core.Enums.PageSegMode.Auto, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
        },
        CacheService,
        OutputService,
        DocumentService,
        TestConfig.PdfFolder3);
    
    private readonly IPdfDataExtractorService _pdfDataExtractorCombined4 = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, Core.Enums.PageSegMode.SparseTextOsd, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, Core.Enums.PageSegMode.Auto, CacheService, OutputService, TestConfig.DotnetPath, TestConfig.TesseractExeName, TestConfig.TesseractExeDirectory),
        },
        CacheService,
        OutputService,
        DocumentService,
        TestConfig.PdfFolder4);
    
    private readonly Dictionary<string, DmsFileData> _fileLicenceMapping = new()
    {
        {
            "28_39_28_312", new DmsFileData
            {
                DmsPath = "ABC",
                DestinationFileName = "DEF"
            }
        }
    };
    private readonly NaldLicenceStatusData _naldLicenceStatusData = new()
    {
        LiveLicences = [],
        DeadLicences = [],
        ImpoundmentLicences = []
    };
    private readonly Dictionary<string, List<NaldData>> _naldData = [];

    private async Task<LookupConfiguration> LookupConfigurationAsync(int regionCode)
    {
        return new LookupConfiguration(
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            await firstNamesFixture.FirstNamesCsvTask(),
            regionCode);
    }
    
    private async Task<MatchesResult> GetMatchesAsync(string fileName, int regionCode, int folderNumber = 1)
    {
        string f;
        IPdfDataExtractorService extractor;

        switch (folderNumber)
        {
            case 1:
                f = TestConfig.PdfFolder;
                extractor = _pdfDataExtractorCombined1;
                break;
            case 3:
                f = TestConfig.PdfFolder3;
                extractor = _pdfDataExtractorCombined3;
                break;
            case 4:
                f = TestConfig.PdfFolder4;
                extractor = _pdfDataExtractorCombined4;
                break;
            default:
                throw new Exception("Number not known");
        }
        
        return await extractor.GetMatchesAsync(
            f + fileName,
            await LookupConfigurationAsync(regionCode),
            [f + fileName],
            0);
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
        Assert.Equal(16, abstractionLimitsResult.LineNumber);
        
        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal(8, abstractionLimitsSection1.Text!.Count);

        Assert.NotNull(abstractionLimitsSection1.SubResults);
        Assert.Single(abstractionLimitsSection1.SubResults!);

        var section1Sub1 = abstractionLimitsSection1.SubResults![0];
        Assert.Equal(8, section1Sub1.SubResults!.Count);

        var linkedLicences = section1Sub1.SubResults.Where(x => x.MatchedLabel?.Name == "LinkedLicenceNumber").ToList();
        Assert.Single(linkedLicences);
        Assert.Equal("14/46/03/0852", linkedLicences[0].Text!.First().Text);
        
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
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        var perYear1 = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("5116", perYear1);
        
        var perYearUnits1 = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits1);
        
        var perYear2 = section1Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("5116", perYear2);
        
        var perYearUnits2 = section1Sub1.SubResults
            .LastOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits2);        
        
        // See notes RE licence
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task Alternate_WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "28-39-28-0312 5606418.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(10, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(8, records.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("CROXLEY HALL WATERS LIMITED", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel?.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count); // TODO should be 5
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/28/312", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Equal("28/39/28/0312", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Equal("ABC", agreedSchemaLicence.DmsPath);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        
        Assert.Equal("28/39/28/507", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("FurtherProvisions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("WhenAddedTo", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }

    [Fact]
    public async Task FaintText_WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Old 6078947.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(8, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var licenceNumber = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumber);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("MID CHESHIRE WATER BOARD", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResult.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.FullyOnSameLine, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(7, abstractionLimitsResult.Text?.Count);
        
        var linkedLicenceNumberCount = resultList.Count(result => result.LabelGroupName == "LinkedLicenceNumber");
        Assert.Equal(12, linkedLicenceNumberCount); // TODO - why not 100s?
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(8, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("25/68/1/153", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("25/68/1/155", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Equal("25/68/1/156", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);
        Assert.Equal("25/68/1/158", agreedSchemaLicence.LinkedLicences[3].LicenceNumber);
        Assert.Equal("25/68/1/180", agreedSchemaLicence.LinkedLicences[4].LicenceNumber);
        Assert.Equal("25/68/1/184", agreedSchemaLicence.LinkedLicences[5].LicenceNumber);
        // TODO add others
    }
    
    [Fact]
    public async Task WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "34_236CA_LICENCE 8463615 (2007).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(19, records.Text!.Count);
        
        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr E C Webb", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel?.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("34/259", licenceNumberResult.Text!.FirstOrDefault()?.Text); // TODO - Dodgy file made up of 2 licences
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("34/259", agreedSchemaLicence.LicenceNumber?.Value);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("34/236CA", agreedSchemaLicence.LinkedLicences[0].LicenceNumber); // TODO actually the wrong way round between this and the main licence number
    }
    
    [Fact]
    public async Task WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "original licence (12.03.1975).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(4, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count); // Licence number gets OCR-ed too scrambled to be read

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Wessex Water Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        // TODO try it with Azure AI Vision
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("First DAY OF", dateOfIssue.Text?.FirstOrDefault()?.Text); // TODO
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("HINTON FARM LIMITED", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("authority hereby licenge", nameResult.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(9, abstractionLimitsResult.Text?.Count);  
        
        // Licence number gets OCRed too scrambled
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Null(agreedSchemaLicence.LicenceNumber);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task JMStrongAndPartners_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence Original 5796052.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(9, records.Text!.Count);
        
        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("J M Strong and Partners", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(7, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.Equal("13/43/021/G/061", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
       
        Assert.Equal("1/34/30/021/G061", agreedSchemaLicence.LicenceNumber?.Value);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task CroxleyHallFarm_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "28-39-28-0507 5609942.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(13, records.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.Equal("10 4", dateOfIssue.Text?.FirstOrDefault()?.Text); // TODO - bit weird
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("CROXLEY HALL WATERS LIMITED", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/28/507", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            [],
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        
        Assert.Equal("28/39/28/312", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("FurtherProvisions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("WhenAddedTo", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task MRJEWard_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Old 6081901.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(19, records.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr J. E. Ward", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["\"the Licence Holder'"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);

        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25 68 002 182", licenceNumberResult.Text!.FirstOrDefault()?.Text);

        var linkedLicences = resultList.Where(result => result.LabelGroupName == "LinkedLicenceNumber").ToList();
        Assert.Equal(2, linkedLicences.Count);
        Assert.Equal("25 68 002 177", linkedLicences[0].Text![0].Text);
        Assert.Equal("25 68 002 182", linkedLicences[1].Text![0].Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        
        Assert.Equal("25/68/002/177", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("Records", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);      
    }

    [Fact]
    public async Task XYZ_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Original Licence 5646512.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text); // National Rivers Authority
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("12th DAY OF DECEMBER 1997", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Lingfield Park 1991 Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["is hereby licensed"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.OnOrNearPreviousLine, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(3, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("3/074", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.Single().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Single().Licences.First();
        Assert.Equal(new DateTime(1997, 12, 12), agreedSchemaLicence.LicenceVersion.IssueDate);
        
        // These arent in NALD, maybe because they were cancelled in 1992 by the NRA?
        //Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length);
        //Assert.Equal("9/40/3/194/SR", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        //Assert.Equal("9/40/3/326/SR", agreedSchemaLicence.LinkedLicences[2].LicenceNumber);

        // Improved - got rid of the false positive match of `3/974`
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task XYZ4_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Original 5798383.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(10, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(8, records.Text!.Count);
        
        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
       var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("E & H Pelham Farms", nameResult.Text?.FirstOrDefault()?.Text); // TODO should be E &H Pelham Farms
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);

        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(10, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("13/43/022/G/033", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task XYZ6_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Non-Application Licence Document Licence document 28112002.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(10, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(8, records.Text!.Count);
        
        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("CN Wookey", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.Equal("13/43/021/G/018", licenceNumberResult.Text![0].Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("1/34/30/021/G018", agreedSchemaLicence.LicenceNumber?.Value);
        
        Assert.Single(agreedSchemaLicenceGroup.Single().Licences);

        Assert.NotNull(agreedSchemaLicence.LicenceNumber);
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }    
    
    [Fact]
    public async Task XYZ5_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Old 6083958.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(8, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count); // The document is printed out of alignment and has ghosting
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(7, abstractionLimitsResult.Text?.Count);
        
        // The document is printed out of alignment and has ghosting
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup); // NOTE - There are a few in this licence, but OCR doesnt read right
        // The one it does read (25/68/5/7) cant be found in NALD
        
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Null(agreedSchemaLicence.LicenceNumber);
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task XYZ2_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Non-Application Licence Document [Licence] (25112008).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("National Rivers Authority", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("J La Trobe Esq", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["is hereby licensed"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.OnOrNearPreviousLine, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");

        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count);

        // TODO doesnt play nicely with the m3 units stuff
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("6/076", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);   
    }
    
    [Fact]
    public async Task XYZ3_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Old 6083584.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        //Assert.Equal(5, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count); // File is scanned titled and font is very bold and hard to read

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("Fifteenth day of March, 19", dateOfIssue.Text?.FirstOrDefault()?.Text); // TODO something
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult);
        
        // TODO: It doesn't match the DB because it misreads as `25/68 5B 8` - not sure if we can improve - perhaps need a fuzzy match?
        // var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        // Assert.NotNull(licenceNumberResult);
        // Assert.True(licenceNumberResult.IsOcr);
        // Assert.Equal("25/68 5B 8", licenceNumberResult.Text!.FirstOrDefault()?.Text); // TODO this actually should have the last 8
        
        // File is scanned titled and font is very bold and hard to read
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First(); // TODO skewwed badly, doesnt read well - there should be 6
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("25/68/3/75", agreedSchemaLicence.LinkedLicences[0].LicenceNumber); // TODO its actually 76!
    }
    
    [Fact(Skip = "Handwritten")]
    public async Task Y_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Original 5809134.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(3, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("N. DIBBEN, ESQ.", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.FullyOnSameLine, nameResult.MatchedPosition);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(3, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }    
    
    [Fact]
    public async Task X_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Non-Application Licence Document (14.11.2000).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(7, records.Text!.Count);
        
        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("New Barn Nurseries", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(6, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("16/52/005/G/411", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task AttachedSticker_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence Original 5652046.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count); // Reads licence number very badly wrong. Doesnt read abstraction limits correctly

        var licenceNumber = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("29/38/1/61", licenceNumber.Text?.FirstOrDefault()?.Text); 
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Lee Conservancy Catchment Board", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("Twentieth day of September, 196. 6", dateOfIssue.Text?.FirstOrDefault()?.Text); // TODO should be 'Twentieth day of September 1966'
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Three Valleys Water Plc", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResult.MatchedLabel!.Text?.Select(x => x.Text)!, StringComparer.InvariantCultureIgnoreCase);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResult.MatchedPosition);
        
        // Reads licence number very badly wrong. Doesn't read abstraction limits correctly
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(35, agreedSchemaLicence.LinkedLicences.Length); // TODO should count these at some point
    }

    [Fact]
    public async Task WhenIsSuccession_ThenNotFound()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Non-Application Licence Document (08.06.1987).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("9th dayof January, 196/", dateOfIssue.Text?.FirstOrDefault()?.Text); // TODO whats the last char
        
        // Success sticker used, company name is OCR-ed
        // scrambled. Rest of document is greyed out slightly and hard to read, including
        // abstraction limits that come out of OCR all scrambled
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.NotNull(nameResult);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/271", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        // Abstraction limits come out of OCR all scrambled
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));

        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
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
        Assert.Equal(7, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count); // Crossed out company name
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult); // Crossed out
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.NotNull(abstractionLimitsResult);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/68/3/91", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("25/68/3/91", agreedSchemaLicence.LicenceNumber?.Value);
        
        Assert.Equal("25/68/5/9", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal("UnknownPage3", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        
        // TODO there are 2 more mentioned in a crosssed section that im not sure why it doesnt read
    }
    
    [Fact]
    public async Task ReallyOldPrinting_WhenCantBeRead_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Application New Licence Issued - 22-07-1966 - 22-07-1966.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(2, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count); // Very old printing, hard to OCR
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult);
        
        // TODO: This one's in the DB as `8/37/43/*G/0033`
        // var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        // Assert.NotNull(licenceNumberResult);
        // Assert.True(licenceNumberResult.IsOcr);
        // Assert.Equal("8/37/43/33", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var issuer = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.Equal("Essex River Authority", issuer!.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        // TODO: Investigate this - no longer found - finds as 8/37/43/*G/0019
        //var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        //Assert.Equal("8/37/43/033", agreedSchemaLicence.LicenceNumber);
        
        // Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length);
        // Assert.Equal("8/37/43/019", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        // Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        // Assert.Equal("Records", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        //
        // Assert.Equal("8/37/03/003", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        // Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        // Assert.Equal("Records", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
    }
    
    [Fact(Skip = "CantLoadImage")]
    public async Task CantLoadImage_NearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "permit_01_01_1998.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(3, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Three Valleys Water Plc", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["hereby grant a licence to"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Empty(abstractionLimitsResult.Text!);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("y", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(3, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length);
        
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task AttachedStickerDifferent_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "2938010008 5641759.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count); // Abstraction limits crossed out

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Lee Conservancy Catchment Board", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Three Valleys Water Plc", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResult.MatchedLabel!.Text!.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResult.MatchedPosition);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("29/38/1/8", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        // Abstraction limitscrossed out
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(35, agreedSchemaLicence.LinkedLicences.Length); // TODO should count these at some point
    }

    [Fact]
    public async Task SingleWordCompany_WhenOcrSameLineSingleWord_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Old 6084155.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(9, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Barrowmore", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnSameLineSingleWord, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(5, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25 68 006 109", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task EstateCompany_WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Non-Application Licence Document (22.05.2001).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(11, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(7, records.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("THE AVIARY ESTATE", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(6, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/38/35", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task Handsigned_WhenCantBeReadByTesseract_ThenDoesNotGiveResult()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Non-Application Licence Document (22.09.1986).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult); // Can't read handwriting

        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(3, abstractionLimitsResult.Text?.Count);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("11/42/28.2/7", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("11/42/28.2/49", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
    }
    
    [Fact]
    public async Task VeryFaintText_WhenCantBeReadByTesseract_ThenDoesNotGiveResult()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Licence - Old 6078942.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(5, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count); // Very faint text
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");

        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("CH Si IRE", nameResult.Text?[0]?.Text); // TODO wrong
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.OnOrNearNextLine, nameResult.MatchedPosition);
        
        // TODO: No longer found because it's looking for the wrong licence number (63 vs 68 segment) which doesn't exist in DB - I suppose it's better than a false match
        // var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        // Assert.NotNull(licenceNumberResult);
        // Assert.True(licenceNumberResult.IsOcr);
        // Assert.Equal("25/63/1/158", licenceNumberResult.Text!.FirstOrDefault()?.Text); // Actually should be 25/68/1/158
        
        // Poor OCR stops us finding the section (its in points)
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        //Assert.Equal("25/63/1/158", agreedSchemaLicence.LicenceNumber); - NULL as doesn't read with OCR correctly
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("25/68/1/1", agreedSchemaLicence.LinkedLicences[0].LicenceNumber); // TODO should be 25/68/1/153
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("UnknownPage1", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        
        // TODO: Investigate linked licences. Looks like quite a few in the PDF so not sure why we're just looking for 1
        //var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        //Assert.Equal("25/63/1/158", agreedSchemaLicence.LicenceNumber);
        // Assert.Single(agreedSchemaLicence.LinkedLicences);
        // Assert.Equal("25/68/1/153", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        // Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        // Assert.Equal("UnknownPage1", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
    }
    
    [Fact]
    public async Task Z_X_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "08-36-19-S-0130 5827009.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(12, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(21, records.Text!.Count);
        
        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        
        var companyName = string.Join(' ', nameResult.Text?.Select(x => x.Text).ToArray()!);
        Assert.Equal("Mr Robert Clifford Abbott and Mrs Rebecca Jane Abbott trading as R P Abbott and Sons", companyName);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchedPosition.EitherSideOfLabel, nameResult.MatchedPosition);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.True(abstractionLimitsSection.IsOcr);
        Assert.Equal(9, abstractionLimitsSection.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("8/36/19/S/130", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        
        Assert.Equal("8/36/19/S/101", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task A1_B2_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Non-Application Licence Document (12.09.1979).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Wessex Water Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        // TODO: It doesn't find this because in the DB it's `13/43/037/S/110` - should we be matching it without the /S/ ?
        // var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        // Assert.NotNull(licenceNumberResult);
        // Assert.True(licenceNumberResult.IsOcr);
        // Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, licenceNumberResult.MatchedLabel!.Position);        
        // Assert.Equal("13/43/37/110", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var additionalInformation = resultList.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.Null(additionalInformation);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task A3_B4_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "Non-Application Licence Document (14.09.1992).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1);
        var resultList = resultFull.Matches!;

        // Assert
        Assert.Equal(6, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Thames Water Authority", issuerResult.Text?.FirstOrDefault()?.Text);

        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("14th day of January, 1976",
            dateOfIssue.Text?.FirstOrDefault()?.Text); // TODO should be dayof ideally

        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");

        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, licenceNumberResult.MatchedLabel!.Position);
        Assert.Equal("28/39/22/427", licenceNumberResult.Text!.FirstOrDefault()?.Text);

        // Name cannot be found as its stricken through (should be 'Barry Ball')

        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined1,
            TestConfig.PdfFolder,
            0,
            await LookupConfigurationAsync(1));

        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Equal("283922427-LVUNKNOWN", agreedSchemaLicenceGroup[0].LicenceSetId);
        Assert.Equal("427", agreedSchemaLicenceGroup[0].ShortLicenceSetId);
        Assert.Equal("283922217-LVUNKNOWN-283922427-LVUNKNOWN", agreedSchemaLicenceGroup[1].LicenceSetId);
        Assert.Equal("217-427", agreedSchemaLicenceGroup[1].ShortLicenceSetId);

        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);

        Assert.Equal("28/39/22/217", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("AbstractionLimits", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
    }
    
    [Fact]
    public async Task AAA3_B4_ThenFoundCorrectly()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12203045__Non-Application Licence Document [Original licence] (23051967).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 4);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(5, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Northumbrian River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        // Tesseract can't get a good result on this
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.Null(dateOfIssue);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Null(licenceNumberResult);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined4,
            TestConfig.PdfFolder4,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Equal("12203045-LVUNKNOWN", agreedSchemaLicenceGroup[0].LicenceSetId);
        Assert.Equal("045", agreedSchemaLicenceGroup[0].ShortLicenceSetId);
        
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task FileWithImageWithSmallDimensions()
    {
        // Arrange
        await SetupLicenceNumbersAsync(1);
        const string filename = "12202043__Licence - Signed Addendum 6431587.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename, 1, 4);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(5, GeneralTestsHelper.ExcludeSomeMatches(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("20 April 2011", dateOfIssue.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, licenceNumberResult.MatchedLabel!.Position);        
        Assert.Equal("1/22/02/043", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await WalSchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _naldLicenceStatusData,
            _naldData,
            _pdfDataExtractorCombined4,
            TestConfig.PdfFolder4,
            0,
            await LookupConfigurationAsync(1));
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Equal("12202043-LV20110419", agreedSchemaLicenceGroup[0].LicenceSetId);
        Assert.Equal("043", agreedSchemaLicenceGroup[0].ShortLicenceSetId);
        
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
}