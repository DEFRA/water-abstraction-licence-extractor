using Tesseract;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using MatchType = WALE.ProcessFile.Core.Enums.MatchType;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

public class TessaractOcrPdfTests
{
    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
 
    private readonly IPdfDataExtractorService _pdfDataExtractorCombined = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.SparseTextOsd, CacheService, OutputService),
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath, PageSegMode.Auto, CacheService, OutputService),
        },
        CacheService,
        OutputService,
        TestConfig.PdfFolder);    
    
    private static string PdfFolder => TestConfig.PdfFolder;

    private readonly Dictionary<string, string> _fileLicenceMapping = new() {{"", ""}};
    private readonly HashSet<string> _liveLicenceNumbers = [];
    private readonly HashSet<string> _deadLicenceNumbers = [];
    private readonly HashSet<string> _impoundmentLicenceNumbers = [];

    private Task<MatchesResult> GetMatchesAsync(string fileName)
    {
        return _pdfDataExtractorCombined.GetMatchesAsync(
            PdfFolder + fileName,
            new LookupConfiguration(
                LabelConfiguration.GetLabels(),
                _fileLicenceMapping),
            [PdfFolder + fileName],
            0);
    }
    
    private static List<LabelGroupResult> ExcludeGeneralList(List<LabelGroupResult> matches)
    {
        return matches.Where(m => m.LabelGroupName != "LinkedLicenceNumber").ToList();
    }
    
    [Fact]
    public async Task WhenNearPreviousLineIsCompany_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "14460030853 licence effective 24.07.2005.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(12, ExcludeGeneralList(resultList).Count);
        // Tesseract struggles to read licence number in header and abstraction limits
        // in this document. Azure AI does read them

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(8, records.Text!.Count);
        
        var points = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");
        Assert.NotNull(points);
        
        var licenceNumber = resultList.Single(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("14/46/03/0852", licenceNumber.Text?.FirstOrDefault()?.Text); // TODO should be 14/46/03/0853
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr T M C Davey", nameResult.Text?[0]?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        
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
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task Alternate_WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "28-39-28-0312 5606418.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(10, ExcludeGeneralList(resultList).Count);

        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(17, records.Text!.Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Environment Agency", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("CROXLEY HALL WATERS LIMITED", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel?.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsInMiddleOfTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count); // TODO should be 5
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/28/312", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        
        Assert.Equal("28/39/28/507", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("Records", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("WhenAddedTo", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("FurtherProvisions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("WhenAddedTo", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason); 
    }

    [Fact]
    public async Task FaintText_WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6078947.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(8, ExcludeGeneralList(resultList).Count);

        var licenceNumber = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("25/68/", licenceNumber.Text?.FirstOrDefault()?.Text); // TODO should be 25/68/1/159
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("MID CHESHIRE WATER BOARD", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResult.MatchedLabel?.Text?.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(7, abstractionLimitsResult.Text?.Count);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "34_236CA_LICENCE 8463615 (2007).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(11, ExcludeGeneralList(resultList).Count);

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
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("34/236", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("34/236", agreedSchemaLicence.LicenceNumber);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("169/1367", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("UnknownPage1", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
    }
    
    [Fact]
    public async Task WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "original licence (12.03.1975).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(4, ExcludeGeneralList(resultList).Count); // Licence number gets OCR-ed too scrambled to be read

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
        Assert.Equal(MatchType.NearNextLineIsMatch, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(5, abstractionLimitsResult.Text?.Count);  
        
        // Licence number gets OCRed too scrambled
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
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
        const string filename = "Licence Original 5796052.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(10, ExcludeGeneralList(resultList).Count);

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
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(7, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.Null(licenceNumberResult); // TODO check if the other services can get it - Tesseract doesnt see it (apart from on the map page)
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        
        Assert.Null(agreedSchemaLicence.LicenceNumber); 
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        
        // NOTE if looking for all linked licence numbers in the document, we will find the licence one in the
        // header that is otherwise not found, as the label text is not read
        
        Assert.Equal("43/43/021/G/061", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("UnknownPage2", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
    }

    [Fact]
    public async Task CroxleyHallFarm_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "28-39-28-0507 5609942.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(11, ExcludeGeneralList(resultList).Count);
        
        var records = resultList.FirstOrDefault(result => result.LabelGroupName == "Records");
        Assert.NotNull(records);
        Assert.Equal(23, records.Text!.Count);
        
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
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/28/307", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        
        Assert.Equal("28/39/28/312", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("Records", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("WhenAddedTo", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("FurtherProvisions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("WhenAddedTo", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);
    }
    
    [Fact]
    public async Task MRJEWard_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6081901.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(12, ExcludeGeneralList(resultList).Count);

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
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);

        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(6, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25 68 002 182", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        
        Assert.Equal("25/68/002/177", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal("Records", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].LinkReason);
        Assert.Equal("FurtherConditions", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].SectionName);
        Assert.Equal("AggregateCondition", agreedSchemaLicence.LinkedLicences[0].ContainedIn![1].LinkReason);        
    }

    [Fact]
    public async Task XYZ_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Original Licence 5646512.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, ExcludeGeneralList(resultList).Count);

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
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(3, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("3/074", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.Single().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal(new DateTime(1997, 12, 12), agreedSchemaLicence.LicenceVersion.IssueDate);
        
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task XYZ4_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Original 5798383.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(10, ExcludeGeneralList(resultList).Count);

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
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);

        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(10, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("13/43/022/G/033", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task XYZ6_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Non-Application Licence Document Licence document 28112002.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(9, ExcludeGeneralList(resultList).Count);
        
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
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(8, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.Null(licenceNumberResult); // For some reason Tesseract won't read the licence number from the box in the header its in
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Null(agreedSchemaLicence.LicenceNumber);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("13/43/021/G/018", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("UnknownPage2", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
    }    
    
    [Fact]
    public async Task XYZ5_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6083958.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, ExcludeGeneralList(resultList).Count); // The document is printed out of alignment and has ghosting
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(7, abstractionLimitsResult.Text?.Count);
        
        // The document is printed out of alignment and has ghosting
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Equal(2, agreedSchemaLicenceGroup.Count);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Null(agreedSchemaLicence.LicenceNumber);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("25/68/5/7", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("UnknownPage5", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
    }
    
    [Fact]
    public async Task XYZ2_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Non-Application Licence Document [Licence] (25112008).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(5, ExcludeGeneralList(resultList).Count);

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("National Rivers Authority", issuerResult.Text?.FirstOrDefault()?.Text);        
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("J La Trobe Esq", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["is hereby licensed"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");

        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count);

        // TODO doesnt play nicely with the m3 units stuff
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("6/076", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task XYZ3_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6083584.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(5, ExcludeGeneralList(resultList).Count); // File is scanned titled and font is very bold and hard to read

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("Fifteenth day of March, 19", dateOfIssue.Text?.FirstOrDefault()?.Text); // TODO something
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/685B8", licenceNumberResult.Text!.FirstOrDefault()?.Text); // TODO this actually should have the last 8
        
        // File is scanned titled and font is very bold and hard to read
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact(Skip = "Handwritten")]
    public async Task Y_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Original 5809134.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(3, ExcludeGeneralList(resultList).Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("N. DIBBEN, ESQ.", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
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
        const string filename = "Non-Application Licence Document (14.11.2000).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(11, ExcludeGeneralList(resultList).Count);
        
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
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(6, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("16/52/005/G/411", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task AttachedSticker_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence Original 5652046.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, ExcludeGeneralList(resultList).Count); // Reads licence number very badly wrong. Doesnt read abstraction limits correctly

        var licenceNumber = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumber);
        Assert.Equal("938/1", licenceNumber.Text?.FirstOrDefault()?.Text); // TODO Should be 9/38/1/61
        
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
        Assert.Equal(MatchType.NearNextLineIsMatch, nameResult.MatchType);
        
        // Reads licence number very badly wrong. Doesn't read abstraction limits correctly
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task WhenIsSuccession_ThenNotFound()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (08.06.1987).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(5, ExcludeGeneralList(resultList).Count);
        
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
        Assert.Null(abstractionLimitsResult);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task WhenIsOldCrossedOut_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6082700.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, ExcludeGeneralList(resultList).Count); // Crossed out company name
        
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
        Assert.Equal("25/68/3/91/", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("2/56/80/309/1", agreedSchemaLicence.LicenceNumber);
    }
    
    [Fact]
    public async Task ReallyOldPrinting_WhenCantBeRead_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application New Licence Issued - 22-07-1966 - 22-07-1966.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(3, ExcludeGeneralList(resultList).Count); // Very old printing, hard to OCR
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("8/37/4.3/33", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var issuer = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.Equal("Essex River Authority", issuer!.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("8/37/4./303/3", agreedSchemaLicence.LicenceNumber);
        
        Assert.Equal(2, agreedSchemaLicence.LinkedLicences.Length);
        Assert.Equal("8/37/43/019", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("Records", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
        
        Assert.Equal("8/37/03/003", agreedSchemaLicence.LinkedLicences[1].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[1].ContainedIn!);
        Assert.Equal("Records", agreedSchemaLicence.LinkedLicences[1].ContainedIn![0].SectionName);
    }
    
    [Fact(Skip = "CantLoadImage")]
    public async Task CantLoadImage_NearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "permit_01_01_1998.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(3, ExcludeGeneralList(resultList).Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Three Valleys Water Plc", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["hereby grant a licence to"], nameResult.MatchedLabel!.Text?.Select(x => x.Text));
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearNextLineIsMatch, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Empty(abstractionLimitsResult.Text!);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("y", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
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
        const string filename = "2938010008 5641759.pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, ExcludeGeneralList(resultList).Count); // Abstraction limits crossed out

        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Lee Conservancy Catchment Board", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Three Valleys Water Plc", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResult.MatchedLabel!.Text!.Select(x => x.Text)!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearNextLineIsMatch, nameResult.MatchType);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("29/38/1/8", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        // Abstraction limitscrossed out
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }

    [Fact]
    public async Task SingleWordCompany_WhenOcrSameLineSingleWord_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6084155.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(9, ExcludeGeneralList(resultList).Count);
        
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
        Assert.Equal(MatchType.SameLineSingleWord, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(5, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25 68 006 109", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task EstateCompany_WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (22.05.2001).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(11, ExcludeGeneralList(resultList).Count);
        
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
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(6, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/38/35", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task Handsigned_WhenCantBeReadByTesseract_ThenDoesNotGiveResult()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (22.09.1986).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
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
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task VeryFaintText_WhenCantBeReadByTesseract_ThenDoesNotGiveResult()
    {
        // Arrange
        const string filename = "Licence - Old 6078942.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, ExcludeGeneralList(resultList).Count); // Very faint text
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Mersey and Weaver River Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");

        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("CH Si IRE", nameResult.Text?[0]?.Text); // TODO wrong
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchType.NearNextLineIsMatch, nameResult.MatchType);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/63/1/158", licenceNumberResult.Text!.FirstOrDefault()?.Text); // Actually should be 25/68/1/158
        
        // Poor OCR stops us finding the section (its in points)
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Equal("25/63/1/158", agreedSchemaLicence.LicenceNumber);
        
        Assert.Single(agreedSchemaLicence.LinkedLicences);
        Assert.Equal("25/68/1/153", agreedSchemaLicence.LinkedLicences[0].LicenceNumber);
        Assert.Single(agreedSchemaLicence.LinkedLicences[0].ContainedIn!);
        Assert.Equal("UnknownPage1", agreedSchemaLicence.LinkedLicences[0].ContainedIn![0].SectionName);
    }
    
    [Fact]
    public async Task Z_X_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "08-36-19-S-0130 5827009.PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(12, ExcludeGeneralList(resultList).Count);
        
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
        Assert.Equal(MatchType.MatchIsEitherSideOfLabel, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.True(abstractionLimitsSection.IsOcr);
        Assert.Equal(8, abstractionLimitsSection.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("8/36/19/S/130", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Equal(3, agreedSchemaLicenceGroup.Count);
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
        const string filename = "Non-Application Licence Document (12.09.1979).pdf";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(7, ExcludeGeneralList(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Wessex Water Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, licenceNumberResult.MatchedLabel!.Position);        
        Assert.Equal("13/43/37/110", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var additionalInformation = resultList.FirstOrDefault(result => result.LabelGroupName == "Additional");
        Assert.Null(additionalInformation);
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
        Assert.Single(agreedSchemaLicenceGroup);
        Assert.Single(agreedSchemaLicenceGroup.First().Licences);

        var agreedSchemaLicence = agreedSchemaLicenceGroup.Last().Licences.First();
        Assert.Empty(agreedSchemaLicence.LinkedLicences);
    }
    
    [Fact]
    public async Task A3_B4_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (14.09.1992).PDF";

        // Act
        var resultFull = await GetMatchesAsync(filename);
        var resultList = resultFull.Matches!;
        
        // Assert
        Assert.Equal(6, ExcludeGeneralList(resultList).Count);
        
        var issuerResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Issuer");
        Assert.NotNull(issuerResult);
        Assert.Equal("Thames Water Authority", issuerResult.Text?.FirstOrDefault()?.Text);
        
        var dateOfIssue = resultFull.Matches!
            .FirstOrDefault(result => result.LabelGroupName == "DateOfIssue");
        Assert.NotNull(dateOfIssue);
        Assert.StartsWith("14th day of January, 1976", dateOfIssue.Text?.FirstOrDefault()?.Text); // TODO should be dayof ideally
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, licenceNumberResult.MatchedLabel!.Position);        
        Assert.Equal("28/39/22/427", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        // Name cannot be found as its stricken through (should be 'Barry Ball')
        
        var agreedSchemaLicenceGroup = await SchemaConverter.ToLicenceSetsAsync(
            resultFull,
            _fileLicenceMapping,
            _impoundmentLicenceNumbers,
            _deadLicenceNumbers,
            _liveLicenceNumbers,
            _pdfDataExtractorCombined,
            TestConfig.PdfFolder,
            0);
        
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
}