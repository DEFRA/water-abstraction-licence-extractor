using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

public class TessaractOcrPdfTests
{
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new TesseractOcrDataExtractorService(TestConfig.TesseractPath)
        },
        TestConfig.PdfFolder);
    
    private static string PdfFolder => TestConfig.PdfFolder;
    private readonly Dictionary<string, string> _fileLicenceMapping = new() {{"", ""}};
    private const bool UseCache = true;

    [Fact]
    public async Task WhenNearPreviousLineIsCompany_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "14460030853 licence effective 24.07.2005.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(4, resultList.Count);
        // Tesseract struggles to read licence number in header and abstraction limits
        // in this document. Azure AI does read them
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr T M C Davey", nameResult.Text?[0]?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(9, abstractionLimitsResult.Text?.Count);
        
        Assert.NotNull(abstractionLimitsResult.SubResults);
        Assert.Single(abstractionLimitsResult.SubResults);
        Assert.Equal(15, abstractionLimitsResult.LineNumber);
        
        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal(9, abstractionLimitsSection1.Text!.Count);

        Assert.NotNull(abstractionLimitsSection1.SubResults);
        Assert.Single(abstractionLimitsSection1.SubResults!);

        var section1Sub1 = abstractionLimitsSection1.SubResults![0];
        Assert.Equal(5, section1Sub1.SubResults!.Count);

        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("77", perDay);

        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        var perYear = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("5116", perYear);
        
        var perYearUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);        
        
        // See notes RE licence
    }
    
    [Fact]
    public async Task Alternate_WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "28-39-28-0312 5606418.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("CROXLEY HALL WATERS LIMITED", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel?.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(5, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/28/312", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }

    [Fact]
    public async Task FaintText_WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6078947.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(3, resultList.Count); // Only 2 as the serial number cannot be read correctly
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("MID CHESHIRE WATER BOARD", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResult.MatchedLabel?.Text!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(7, abstractionLimitsResult.Text?.Count);
        
        // The serial number cannot be read correctly
    }
    
    [Fact]
    public async Task WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "34_236CA_LICENCE 8463615 (2007).pdf";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr E C Webb", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel?.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("34/236", licenceNumberResult.Text!.FirstOrDefault()?.Text);        
    }
    
    [Fact]
    public async Task WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "original licence (12.03.1975).PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(2, resultList.Count); // Licence number gets OCR-ed too scrambled to be read
        // TODO try it with Azure AI Vision
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("HINTON FARM LIMITED", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("authority hereby licenge", nameResult.MatchedLabel?.Text!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel?.Position);
        Assert.Equal(MatchType.NearNextLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(7, abstractionLimitsResult.Text?.Count);  
        
        // Licence number gets OCRed too scrambled
    }

    [Fact]
    public async Task JMStrongAndPartners_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence Original 5796052.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("J M Strong and Partners", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(9, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("13/43/021/G/061", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }

    [Fact]
    public async Task CroxleyHallFarm_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "28-39-28-0507 5609942.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("CROXLEY HALL WATERS LIMITED", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(4, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/28/507", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task MRJEWard_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6081901.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr J. E. Ward", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["\"the Licence Holder\""], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);

        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(7, abstractionLimitsResult.Text?.Count);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25 68 002 182", licenceNumberResult.Text!.FirstOrDefault()?.Text);      
    }

    [Fact]
    public async Task XYZ_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Original Licence 5646512.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(4, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Lingfield Park 1991 Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["is hereby licensed"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(3, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("3/974", licenceNumberResult.Text!.FirstOrDefault()?.Text);        
    }

    [Fact]
    public async Task XYZ4_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Original 5798383.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("E & H Pelham Farms", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(12, abstractionLimitsResult.Text?.Count);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("13/43/022/G/033", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }

    [Fact]
    public async Task XYZ6_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Non-Application Licence Document Licence document 28112002.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(4, resultList.Count);
        // For some reason it won't read the licence number
        // from the box in the header its in
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("CN Wookey", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(9, abstractionLimitsResult.Text?.Count);
        
        // For some reason it won't read the licence number
        // from the box in the header its in
    }    
    
    [Fact]
    public async Task XYZ5_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6083958.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(2, resultList.Count); // The document is printed out of alignment and has ghosting
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(2, abstractionLimitsResult.Text?.Count);
        
        // The document is printed out of alignment and has ghosting
    }
    
    [Fact]
    public async Task XYZ2_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Non-Application Licence Document [Licence] (25112008).PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(4, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("J La Robe Esq", nameResult.Text?.FirstOrDefault()?.Text); // TODO this actually should be Trobe
        Assert.Equal(["is hereby licensed"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(5, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("6/076", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task XYZ3_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6083584.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(2, resultList.Count); // File is scanned titled and font is very bold and hard to read

        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/685B", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        // File is scanned titled and font is very bold and hard to read        
    }
    
    [Fact(Skip = "Handwritten")]
    public async Task Y_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Original 5809134.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(3, resultList.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("N. DIBBEN, ESQ.", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
    }    
    
    [Fact]
    public async Task X_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (14.11.2000).PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("New Barn Nurseries", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(6, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("16/52/005/G/411", licenceNumberResult.Text!.FirstOrDefault()?.Text);        
    }
    
    [Fact]
    public async Task AttachedSticker_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence Original 5652046.pdf";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(3, resultList.Count); // Reads licence number very badly wrong. Doesnt read abstraction limits correctly
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Three Valleys Water Plc", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResult.MatchedLabel!.Text!, StringComparer.InvariantCultureIgnoreCase);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearNextLineIsCompany, nameResult.MatchType);
        
        // Reads licence number very badly wrong. Doesn't read abstraction limits correctly
    }

    [Fact]
    public async Task WhenIsSuccession_ThenNotFound()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (08.06.1987).PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(3, resultList.Count);
        
        // Success sticker used, company name is OCR-ed
        // scrambled. Rest of document is greyed out slightly and hard to read, including
        // abstraction limits that come out of OCR all scrambled
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/271", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        // Abstraction limits come out of OCR all scrambled
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        Assert.Null(abstractionLimitsResult);        
    }

    [Fact]
    public async Task WhenIsOldCrossedOut_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6082700.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(2, resultList.Count); // Crossed out company name
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult); // Crossed out
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.Null(abstractionLimitsResult); // Crossed out
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/68/3/91", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task ReallyOldPrinting_WhenCantBeRead_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application New Licence Issued - 22-07-1966 - 22-07-1966.pdf";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Single(resultList); // Very old printing, hard to OCR
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("8/37/4.3/33", licenceNumberResult.Text!.FirstOrDefault()?.Text);        
    }
    
    [Fact(Skip = "CantLoadImage")]
    public async Task CantLoadImage_NearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "permit_01_01_1998.pdf";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(3, resultList.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Three Valleys Water Plc", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["hereby grant a licence to"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearNextLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(0, abstractionLimitsResult.Text?.Count);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("y", licenceNumberResult.Text!.FirstOrDefault()?.Text);        
    }
    
    [Fact]
    public async Task AttachedStickerDifferent_WhenNearNextLineIsCompany_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "2938010008 5641759.pdf";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(4, resultList.Count); // Abstraction limitscrossed out

        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Three Valleys Water Plc", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Contains("hereby grant a licence to", nameResult.MatchedLabel!.Text!);
        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearNextLineIsCompany, nameResult.MatchType);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("29/38/1/8", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        // Abstraction limitscrossed out
    }

    [Fact]
    public async Task SingleWordCompany_WhenOcrSameLineSingleWord_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Licence - Old 6084155.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Barrowmore", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineSingleWord, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(5, abstractionLimitsResult.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25 68 006 109", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task EstateCompany_WhenOcrSameLineIsCompany1Line_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (22.05.2001).PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("THE AVIARY ESTATE", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.True(abstractionLimitsResult.IsOcr);
        Assert.Equal(6, abstractionLimitsResult.Text?.Count);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/38/35", licenceNumberResult.Text!.FirstOrDefault()?.Text);         
    }
    
    [Fact]
    public async Task Handsigned_WhenCantBeReadByTesseract_ThenDoesNotGiveResult()
    {
        // Arrange
        const string filename = "Non-Application Licence Document (22.09.1986).PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
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
    }
    
    [Fact]
    public async Task VeryFaintText_WhenCantBeReadByTesseract_ThenDoesNotGiveResult()
    {
        // Arrange
        const string filename = "Licence - Old 6078942.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(3, resultList.Count); // Very faint text
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Null(nameResult); // Very faint text
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("25/68/1/158", licenceNumberResult.Text!.FirstOrDefault()?.Text);        
    }
    
    [Fact]
    public async Task Z_X_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "08-36-19-S-0130 5827009.PDF";

        // Act
        var resultList = (await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            _fileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache)).Matches!;
        
        // Assert
        Assert.Equal(5, resultList.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.True(nameResult.IsOcr);
        Assert.Equal("Mr Robert Clifford Abbott and Mrs Rebecca Jane Abbott trading as R P Abbott and Sons", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany2Lines, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.True(abstractionLimitsSection.IsOcr);
        Assert.Equal(7, abstractionLimitsSection.Text?.Count);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.True(licenceNumberResult.IsOcr);
        Assert.Equal("8/36/19/S/130", licenceNumberResult.Text!.FirstOrDefault()?.Text);        
    }
}