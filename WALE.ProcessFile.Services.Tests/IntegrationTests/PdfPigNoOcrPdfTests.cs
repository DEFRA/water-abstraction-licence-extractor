using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

public class PdfPigNoOcrPdfTests
{
    private readonly IPdfDataExtractorService _pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>(),
        PdfFolder);

    private static string PdfFolder => TestConfig.PdfFolder;
    private const bool UseCache = false;
    
    private static Dictionary<string, string> FileLicenceMapping =>
        new()
        {
            {
                "25 68 001 247",
                "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10892721.pdf"
            },
            {
                "25 68 001 248",
                "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893422.pdf"
            }
        };

    [Fact]
    public async Task WhenX_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application –Transfer– Issued Licence –05072022.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Ingleby Greenhow Water Society Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        Assert.Equal(165, nameResult.LineNumber);
        
        // Note no other licence mentioned
        var abstractionLimitsSecrtion = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSecrtion);
        Assert.False(abstractionLimitsSecrtion.IsOcr);
        Assert.Equal(5, abstractionLimitsSecrtion.Text?.Count);
        Assert.Equal("A day means any period of 24 consecutive hours and a year means the", abstractionLimitsSecrtion.Text![3].Text);
        Assert.Equal(234, abstractionLimitsSecrtion.LineNumber);
        
        Assert.NotNull(abstractionLimitsSecrtion.SubResults);
        Assert.Single(abstractionLimitsSecrtion.SubResults!);
        Assert.Equal(234, abstractionLimitsSecrtion.LineNumber);
        
        var abstractionLimitsPoint1 = abstractionLimitsSecrtion.SubResults[0];
        Assert.Single(abstractionLimitsPoint1.SubResults!);
        var point1Sub1 = abstractionLimitsPoint1.SubResults![0];
        
        Assert.Equal(5, point1Sub1.Text!.Count);

        Assert.NotNull(point1Sub1.SubResults);
        Assert.Equal(4, point1Sub1.SubResults!.Count);

        var perDay = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(236, perDay.LineNumber);
        Assert.Equal("90.91", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        var perYear = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("33182", perYear);
        
        var perYearUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("1/25/04/059", licenceNumberResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(158, licenceNumberResult.LineNumber);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal("Private Water Supply  Agriculture (other than Spray Irrigation)",
            string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!));
        Assert.Equal(["PURPOSES OF ABSTRACTION"], purposeResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchType.Between, purposeResult.MatchType);
        
        Assert.Equal(2, purposeResult.SubResults!.Count);
        var firstPurpose = purposeResult.SubResults!.First();
        Assert.Equal("Private Water Supply", firstPurpose.Text!.First().Text);

        var secondPurpose = purposeResult.SubResults!.Last();
        Assert.Equal("Agriculture (other than Spray Irrigation)", secondPurpose.Text!.First().Text);        
    }

    [Fact]
    public async Task LongLicenceHolderName_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application - Minor Variation -Application New Licence Issued 24_12_2019 00_00_00 11164372.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Lady Isabelle Jacqueline Laline Hay, Countess of Erroll, Sir Thomas Minshull Stockdale, 2nd Baronet Stockdale, Robert Elkington",
            string.Join(", ", nameResult.Text!.Select(x => x.Text)));
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position); // TODO should eventually be LabelIsInMiddleOfTextToFind
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);

        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/0422", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.False(abstractionLimitsResult.IsOcr);
        Assert.Equal(6, abstractionLimitsResult.Text?.Count);
        Assert.Equal(221, abstractionLimitsResult.LineNumber);
        
        Assert.NotNull(abstractionLimitsResult.SubResults);        
        Assert.Equal(2, abstractionLimitsResult.SubResults.Count);
        Assert.Equal(221, abstractionLimitsResult.LineNumber);
        
        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal(2, abstractionLimitsSection1.Text!.Count);

        Assert.NotNull(abstractionLimitsSection1.SubResults);
        
        Assert.Single(abstractionLimitsSection1.SubResults);
        var section1Sub1 = abstractionLimitsSection1.SubResults![0];
        Assert.Equal(4, section1Sub1.SubResults!.Count);

        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(223, perDay.LineNumber);
        Assert.Equal("205", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perHour = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("41", perHour);
        
        var perHourUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsResult.SubResults[1];
        Assert.Equal(4, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        
        Assert.Single(abstractionLimitsSection2.SubResults);
        var section2Sub1 = abstractionLimitsSection2.SubResults![0];
        
        Assert.Equal(4, section2Sub1.SubResults!.Count);  
        
        var perYear = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("6138", perYear);
        
        var perYearUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var linkedLicenceNumber = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("28/39/22/0338", linkedLicenceNumber);

        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal("Spray irrigation (other than spray irrigation under glass)", purposeResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["PURPOSE OF ABSTRACTION"], purposeResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchType.Between, purposeResult.MatchType);
        
        Assert.Single(purposeResult.SubResults!);
        var firstPurpose = purposeResult.SubResults!.Single();
        Assert.Equal("Spray irrigation (other than spray irrigation under glass)", firstPurpose.Text!.Single().Text);
    }

    [Fact]
    public async Task X_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application – Transfer – Issued Licence – 07.07.2022.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("T Wilson & Sons (Farmers)", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("NW/069/0025/091/R01", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsResult = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsResult);
        Assert.False(abstractionLimitsResult.IsOcr);
        Assert.Equal(15, abstractionLimitsResult.Text?.Count);
        Assert.Equal(264, abstractionLimitsResult.LineNumber);
        
        Assert.NotNull(abstractionLimitsResult.SubResults);       
        
        Assert.Equal(2, abstractionLimitsResult.SubResults.Count);
        Assert.Equal(264, abstractionLimitsResult.LineNumber);
        
        var abstractionLimitsSection1 = abstractionLimitsResult.SubResults[0];
        Assert.Equal(4, abstractionLimitsSection1.Text!.Count);

        Assert.NotNull(abstractionLimitsSection1.SubResults);
        Assert.Single(abstractionLimitsSection1.SubResults);
        
        var section1Sub1 = abstractionLimitsSection1.SubResults[0];
        
        Assert.Equal(8, section1Sub1.SubResults!.Count);
        
        var perHour = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("39.5", perHour);
        
        var perHourUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(266, perDay.LineNumber);
        Assert.Equal("948", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perYear = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per year")));

        Assert.NotNull(perYear);
        Assert.Equal(267, perYear.LineNumber);
        Assert.Equal("40000", perYear.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsResult.SubResults[1];
        Assert.Equal(11, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults!);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(12, section2Sub1.SubResults!.Count); // TODO failing because can't find the linked licences         
            
        perHour = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("39.5", perHour);
        
        perHourUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(274, perDay.LineNumber);
        Assert.Equal("948", perDay.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perYear2 = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("40000", perYear2);
        
        perYearUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var perSecond = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("10.97", perSecond);
        
        var perSecondUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var linkedLicences = section2Sub1.SubResults
            .Where(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")
            .ToList();
        
        Assert.Equal(3, linkedLicences.Count);
        
        var linkedLicenceNumber1 = linkedLicences[0].Text?[0].Text;
        Assert.Equal("NW/069/0025/006/R01", linkedLicenceNumber1); 
        
        var linkedLicenceNumber2 = linkedLicences[1].Text?[0].Text;
        Assert.Equal("NW/069/0025/007/R01", linkedLicenceNumber2);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal(
            "Spray irrigation, subject to the compensatory discharges from the borehole referred to in condition 9.1 below",
            string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!));
        Assert.Equal(["PURPOSE OF ABSTRACTION"], purposeResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchType.Between, purposeResult.MatchType);
        
        Assert.Single(purposeResult.SubResults!);
        var firstPurpose = purposeResult.SubResults!.First();
        Assert.Equal(
            "Spray irrigation, subject to the compensatory discharges from the borehole referred to in condition 9.1 below",
            string.Join(' ', firstPurpose.Text!.Select(x => x.Text).ToArray()));
    }
    
    [Fact]
    public async Task LicenceToCharity_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application new Issued licence 04052017 AN0300012011 9781525.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("The Bourne United Charities", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("AN/030/0012/011", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(11, abstractionLimitsSection.Text?.Count);
        Assert.Equal(246, abstractionLimitsSection.LineNumber);
        
        Assert.NotNull(abstractionLimitsSection.SubResults);        
        Assert.Equal(2, abstractionLimitsSection.SubResults.Count);

        var sectionPoint1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(sectionPoint1.SubResults!);
        
        var section1Sub1 = sectionPoint1.SubResults![0];
        Assert.Equal(8, section1Sub1.SubResults!.Count);
        Assert.Equal(250, section1Sub1.LineNumber);
        
        //var abstractionLimitsSection1 = section1Sub1.SubResults[0];
        Assert.Equal(4, section1Sub1.Text!.Count);

        Assert.NotNull(section1Sub1.SubResults);
        Assert.Equal(8, section1Sub1.SubResults!.Count);
        
        var perHour = section1Sub1.SubResults!
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("55", perHour);
        
        var perHourUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var perDay = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(248, perDay.LineNumber);
        Assert.Equal("409.5", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perYear = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per year")));

        Assert.NotNull(perYear);
        Assert.Equal(249, perYear.LineNumber);
        Assert.Equal("20457", perYear.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);

        var perSecond = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per second")));

        Assert.NotNull(perSecond);
        Assert.Equal(250, perSecond.LineNumber);
        Assert.Equal("15.2", perSecond.Text?.FirstOrDefault()?.Text);
            
        var perSecondUnits = section1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(7, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults!);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(4, section2Sub1.SubResults!.Count);
        
        var perYear2 = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("22730", perYear2);
        
        perYearUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var linkedLicenceNumber = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("4/30/12/*G/0214", linkedLicenceNumber);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        
        Assert.Equal("Spray irrigation, subject to the compensatory discharge of water from the borehole at TF 14084"
            + " 23479 authorised under licence serial number 4/30/12/*G/0214 referred to in Condition 9 below",
            string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!));
        Assert.Equal(["PURPOSE OF ABSTRACTION"], purposeResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchType.Between, purposeResult.MatchType);
        
        Assert.Single(purposeResult.SubResults!);
        var firstPurpose = purposeResult.SubResults!.Single();
        Assert.Equal("Spray irrigation, subject to the compensatory discharge of water from the borehole at TF 14084"
            + " 23479 authorised under licence serial number 4/30/12/*G/0214 referred to in Condition 9 below",
            string.Join(' ', firstPurpose.Text?.Select(x => x.Text).ToArray()!));
    }
    
    [Fact]
    public async Task EWPorterAndSon_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application - NA Formal Variation - Issued Licence [26_3_21] 11759321.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("E.W.Porter and Son", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);

        var abstractionLimitsSection = resultList.FirstOrDefault(result =>
            result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(55, abstractionLimitsSection.Text?.Count);
        
        Assert.NotNull(abstractionLimitsSection.SubResults);
        Assert.Equal(10, abstractionLimitsSection.SubResults.Count);
        Assert.Equal(255, abstractionLimitsSection.LineNumber);
        
        var point1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(point1.SubResults!);
        Assert.Equal(3, point1.Text!.Count);

        var point1Sub1 = point1.SubResults![0];
        Assert.NotNull(point1Sub1.SubResults);
        Assert.Equal(4, point1Sub1.SubResults!.Count);
        
        var perHour = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("46", perHour);
        
        var perHourUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        var perSecond = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("12.7", perSecond);
        
        var perSecondUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(3, abstractionLimitsSection2.Text!.Count);

        Assert.Single(abstractionLimitsSection2.SubResults!);

        var section2Sub1 = abstractionLimitsSection2.SubResults![0];
            
        Assert.NotNull(section2Sub1.SubResults);            
        Assert.Equal(4, section2Sub1.SubResults!.Count);
        
        perHour = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("46", perHour);
        
        perHourUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("19.2", perSecond);
        
        perSecondUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection3 = abstractionLimitsSection.SubResults[2];
        Assert.Equal(3, abstractionLimitsSection3.Text!.Count);

        Assert.NotNull(abstractionLimitsSection3.SubResults);
        Assert.Single(abstractionLimitsSection3.SubResults!);
        
        var section3Sub1 = abstractionLimitsSection3.SubResults![0];
        Assert.Equal(4, section3Sub1.SubResults!.Count);
        
        perHour = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("69", perHour);
        
        perHourUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("19.2", perSecond);
        
        perSecondUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection4 = abstractionLimitsSection.SubResults[3];
        Assert.Equal(3, abstractionLimitsSection4.Text!.Count);

        Assert.NotNull(abstractionLimitsSection4.SubResults);
        Assert.Single(abstractionLimitsSection4.SubResults!);

        var section4Sub1 = abstractionLimitsSection4.SubResults[0];
        Assert.Equal(4, section4Sub1.SubResults!.Count);
        
        perHour = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("137", perHour);
        
        perHourUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("38.1", perSecond);
        
        perSecondUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection5 = abstractionLimitsSection.SubResults[4];
        Assert.Equal(3, abstractionLimitsSection5.Text!.Count);

        Assert.NotNull(abstractionLimitsSection5.SubResults);
        Assert.Single(abstractionLimitsSection5.SubResults!);

        var section5Sub1 = abstractionLimitsSection5.SubResults![0];
        Assert.Equal(4, section5Sub1.SubResults!.Count);
        
        perHour = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("69", perHour);
        
        perHourUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("19.2", perSecond);
        
        perSecondUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection6 = abstractionLimitsSection.SubResults[5];
        Assert.Equal(3, abstractionLimitsSection6.Text!.Count);

        Assert.NotNull(abstractionLimitsSection6.SubResults);
        Assert.Single(abstractionLimitsSection6.SubResults!);

        var section6Sub1 = abstractionLimitsSection6.SubResults[0];
        Assert.Equal(4, section6Sub1.SubResults!.Count);
        
        perHour = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("91", perHour);
        
        perHourUnits = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);

        perSecond = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("25.3", perSecond);
        
        perSecondUnits = section6Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);

        var abstractionLimitsSection7 = abstractionLimitsSection.SubResults[6];
        Assert.Equal(6, abstractionLimitsSection7.Text!.Count);

        Assert.NotNull(abstractionLimitsSection7.SubResults);
        Assert.Single(abstractionLimitsSection7.SubResults!);

        var section7Sub1 = abstractionLimitsSection7.SubResults[0];
        Assert.Equal(4, section7Sub1.SubResults!.Count);
        
        var perDay = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("1440", perDay);
        
        var perDayUnits = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        var perYear = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("22862", perYear);
        
        var perYearUnits = section7Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);                                
        
        var abstractionLimitsSection8 = abstractionLimitsSection.SubResults[7];
        Assert.Equal(6, abstractionLimitsSection8.Text!.Count);

        Assert.NotNull(abstractionLimitsSection8.SubResults);
        Assert.Single(abstractionLimitsSection8.SubResults!);

        var section8Sub1 = abstractionLimitsSection8.SubResults[0];
        Assert.Equal(8, section8Sub1.SubResults!.Count);
        
        perHour = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("251", perHour);
        
        perHourUnits = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("4091", perDay);
        
        perDayUnits = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        perYear = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("190000", perYear);
        
        perYearUnits = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var linkedLicenceNumber = section8Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("6/33/56/*G/0274/R02", linkedLicenceNumber);
        
        var abstractionLimitsSection9 = abstractionLimitsSection.SubResults[8];
        Assert.Equal(5, abstractionLimitsSection9.Text!.Count);

        Assert.NotNull(abstractionLimitsSection9.SubResults);
        Assert.Single(abstractionLimitsSection9.SubResults!);

        var section9Sub1 = abstractionLimitsSection9.SubResults[0];
        Assert.Equal(7, section9Sub1.SubResults!.Count);
        
        perHour = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("46", perHour);
        
        perHourUnits = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("1091", perDay);
        
        perDayUnits = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        perYear = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("40900", perYear);
        
        perYearUnits = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        linkedLicenceNumber = section9Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("6/33/56/*G/0274/R02", linkedLicenceNumber);
        
        var abstractionLimitsSection10 = abstractionLimitsSection.SubResults[9];
        Assert.Equal(11, abstractionLimitsSection10.Text!.Count);

        Assert.NotNull(abstractionLimitsSection10.SubResults);
        Assert.Single(abstractionLimitsSection10.SubResults!);

        var section10Sub1 = abstractionLimitsSection10.SubResults[0];
        Assert.Equal(8, section10Sub1.SubResults!.Count);
        
        perHour = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("205", perHour);
        
        perHourUnits = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perDay = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("3000", perDay);
        
        perDayUnits = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);

        perYear = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("190000", perYear);
        
        perYearUnits = section10Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
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
        Assert.Equal("4.1 Trickle irrigation 4.2 Filling a reservoir for subsequent trickle irrigation", allText);

        Assert.Equal(["PURPOSES OF ABSTRACTION"], purposeResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchType.Between, purposeResult.MatchType);
        
        Assert.Single(purposeResult.SubResults!);
        var purposePointGroup = purposeResult.SubResults!.Single();
        Assert.Equal("PurposePointGroup", purposePointGroup.MatchedLabel!.Name);

        var purposePointGroupSubResults = purposePointGroup.SubResults;
        Assert.Equal(2, purposePointGroupSubResults!.Count);

        var purpose1 = purposePointGroupSubResults[0];
        Assert.Equal("4.1 Trickle irrigation",
            string.Join(' ', purpose1.Text?.Select(x => x.Text).ToArray()!));

        var purpose2 = purposePointGroupSubResults[1];
        Assert.Equal("4.2 Filling a reservoir for subsequent trickle irrigation",
            string.Join(' ', purpose2.Text?.Select(x => x.Text).ToArray()!));
    }

    [Fact]
    public async Task WalderseyFarmsLimited_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application – Renewal – Licence Issued – 24062022.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Waldersey Farms Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("6/33/47/*S/0172/R01", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(32, abstractionLimitsSection.Text?.Count);
        Assert.Equal(5, abstractionLimitsSection.SubResults!.Count);
        Assert.Equal(4, abstractionLimitsSection.SubResults[0].Text!.Count);
        
        Assert.NotNull(abstractionLimitsSection.SubResults);
        Assert.Equal(5, abstractionLimitsSection.SubResults!.Count);
        Assert.Equal(277, abstractionLimitsSection.LineNumber);
        
        var section1Point1 = abstractionLimitsSection.SubResults[0];
        Assert.Equal(4, section1Point1.Text!.Count);
        Assert.NotNull(section1Point1.SubResults);
        Assert.Single(section1Point1.SubResults);
        
        var point1Sub1 = section1Point1.SubResults![0];
        Assert.Equal(6, point1Sub1.SubResults!.Count);

        var perDay = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(280, perDay.LineNumber);
        Assert.Equal("2000", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perHour = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("83", perHour);
        
        var perHourUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        var perSecond = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per second")));

        Assert.NotNull(perSecond);
        Assert.Equal(281, perSecond.LineNumber);
        Assert.Equal("23.1", perSecond.Text?.FirstOrDefault()?.Text);
            
        var perSecondUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(5, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults!);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(2, section2Sub1.SubResults!.Count);
        
        var perYear = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per year")));

        Assert.NotNull(perYear);
        Assert.Equal(284, perYear.LineNumber);
        Assert.Equal("68000", perYear.Text?.FirstOrDefault()?.Text);
        
        var perYearUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var abstractionLimitsSection3 = abstractionLimitsSection.SubResults[2];
        Assert.Equal(2, abstractionLimitsSection3.Text!.Count);

        Assert.NotNull(abstractionLimitsSection3.SubResults);
        Assert.Single(abstractionLimitsSection3.SubResults!);

        var section3Sub1 = abstractionLimitsSection3.SubResults[0];
        Assert.Equal(2, section3Sub1.SubResults!.Count);
        
        perYear = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per year")));

        Assert.NotNull(perYear);
        Assert.Equal(291, perYear.LineNumber);
        Assert.Equal("68000", perYear.Text?.FirstOrDefault()?.Text);
        
        perYearUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        var abstractionLimitsSection4 = abstractionLimitsSection.SubResults[3];
        Assert.Equal(5, abstractionLimitsSection4.Text!.Count);

        Assert.NotNull(abstractionLimitsSection4.SubResults);
        Assert.Single(abstractionLimitsSection4.SubResults!);

        var section4Sub1 = abstractionLimitsSection4.SubResults[0];
        Assert.Equal(8, section4Sub1.SubResults!.Count);

        perHour = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("219", perHour);
        
        perHourUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);
        
        perYear = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per year")));

        Assert.NotNull(perYear);
        Assert.Equal(296, perYear.LineNumber);
        Assert.Equal("61200", perYear.Text?.FirstOrDefault()?.Text);
        
        perYearUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);   
        
        perDay = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(295, perDay.LineNumber);
        Assert.Equal("5256", perDay.Text?.FirstOrDefault()?.Text);

        perDayUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var abstractionLimitsSection5 = abstractionLimitsSection.SubResults[4];
        Assert.Equal(12, abstractionLimitsSection5.Text!.Count);

        Assert.NotNull(abstractionLimitsSection5.SubResults);
        Assert.Single(abstractionLimitsSection5.SubResults!);

        var section5Sub1 = abstractionLimitsSection5.SubResults[0];
        Assert.Equal(10, section5Sub1.SubResults!.Count);

        perYear = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per year")));

        Assert.NotNull(perYear);
        Assert.Equal(304, perYear.LineNumber);
        Assert.Equal("68000", perYear.Text?.FirstOrDefault()?.Text);
        
        perYearUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per year")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perYearUnits);
        
        perHour = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("219", perHour);
        
        perHourUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per hour")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perHourUnits);                        
        
        perDay = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(303, perDay.LineNumber);
        Assert.Equal("5256", perDay.Text?.FirstOrDefault()?.Text);

        perDayUnits = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var linkedLicenceNumber = section5Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicenceNumber")?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("AN/033/0047/018", linkedLicenceNumber);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);

        var allPurposeText = string.Join(' ', purposeResult.Text?.Select(x => x.Text).ToArray()!);
        
        Assert.Equal("4.1 From Point 2.1  Transfer for subsequent discharge and re-abstraction for spray irrigation from" 
            + " the points specified in condition 2.2 of this licence and points specified in"
            + " condition 2.1 of licence AN/033/0047/018"
            + "  4.2 Filling a reservoir for subsequent spray irrigation"
            + "  4.3 From Point 2.2"
            + "  Spray Irrigation",
            allPurposeText); // TODO - why doesnt this have 4.1 and 4.2 in it?
        
        Assert.Equal(["PURPOSES OF ABSTRACTION"], purposeResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchType.Between, purposeResult.MatchType);
        
        Assert.Equal(2, purposeResult.SubResults!.Count);
        
        var firstPurposePointGroup = purposeResult.SubResults[0];
        Assert.Equal(3, firstPurposePointGroup.SubResults!.Count); // 1 point number, 2 lots of text
        
        var purposeGroupName = firstPurposePointGroup.SubResults![0];
        Assert.Equal("2.1", purposeGroupName.Text![0].Text);

        var firstPurpose = firstPurposePointGroup.SubResults![1];
        var firstPurposeAllText = string.Join(' ', firstPurpose.Text?.Select(x => x.Text).ToArray()!);
        
        Assert.Equal("4.1  Transfer for subsequent discharge and re-abstraction for spray irrigation from"
                 + " the points specified in condition 2.2 of this licence and points specified in"
                 + " condition 2.1 of licence AN/033/0047/018",
            firstPurposeAllText);

        var firstPurposeSubs = firstPurpose.SubResults;
        Assert.NotNull(firstPurposeSubs);
        Assert.Equal(3, firstPurposeSubs.Count);
        
        var firstPurposePoint = firstPurposeSubs[0].Text!.Single().Text;
        Assert.Equal("2.1", firstPurposePoint);
        
        var firstPurposePurposeNumber = firstPurposeSubs[1].Text!.Single().Text;
        Assert.Equal("4.1", firstPurposePurposeNumber);
        
        var firstPurposeTextOnly = string.Join(' ', firstPurposeSubs[2].Text?.Select(x => x.Text).ToArray()!);
        Assert.Equal("Transfer for subsequent discharge and re-abstraction for spray irrigation from" 
            + " the points specified in condition 2.2 of this licence and points specified in"
            + " condition 2.1 of licence AN/033/0047/018", firstPurposeTextOnly);
        
        var secondPurpose = firstPurposePointGroup.SubResults![2];
        var secondPurposeAllTText = secondPurpose.Text?.Select(x => x.Text).ToArray()!;
        
        Assert.Equal("4.2 Filling a reservoir for subsequent spray irrigation",
            string.Join(' ', secondPurposeAllTText));

        var secondPurposeSubs = secondPurpose.SubResults;
        Assert.NotNull(secondPurposeSubs);
        Assert.Equal(2, secondPurposeSubs.Count);
        
        var secondPurposePurposeNumber = secondPurposeSubs[0].Text!.Single().Text;
        Assert.Equal("4.2", secondPurposePurposeNumber);
        
        var secondPurposeTextOnly = string.Join(' ', secondPurposeSubs[1].Text?.Select(x => x.Text).ToArray()!);
        Assert.Equal("Filling a reservoir for subsequent spray irrigation", secondPurposeTextOnly);
        
        var secondPurposePointGroup = purposeResult.SubResults[1];        
        
        purposeGroupName = secondPurposePointGroup.SubResults![0];
        Assert.Equal("2.2", purposeGroupName.Text![0].Text);
        
        var thirdPurpose = secondPurposePointGroup.SubResults![1];
        var thirdPurposeAllText = string.Join(' ', thirdPurpose.Text?.Select(x => x.Text).ToArray()!);
        
        Assert.Equal("4.3  Spray Irrigation", thirdPurposeAllText);
        
        var thirdPurposeSubs = thirdPurpose.SubResults;
        Assert.NotNull(thirdPurposeSubs);
        Assert.Equal(3, thirdPurposeSubs.Count);
        
        var thirdPurposePoint = thirdPurposeSubs[0].Text!.Single().Text;
        Assert.Equal("2.2", thirdPurposePoint);
        
        var thirdPurposePurposeNumber = thirdPurposeSubs[1].Text!.Single().Text;
        Assert.Equal("4.3", thirdPurposePurposeNumber);
        
        var thirdPurposeTextOnly = string.Join(' ', thirdPurposeSubs[2].Text?.Select(x => x.Text).ToArray()!);
        Assert.Equal("Spray Irrigation", thirdPurposeTextOnly);
        
        // TODO update config to relate purposes to points
        
        var pointResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Points");

        Assert.NotNull(pointResult);
        Assert.False(pointResult.IsOcr);
        Assert.Equal("DocumentPoints", pointResult.MatchedLabel!.Name);
        
        Assert.Equal(58, pointResult.Text!.Count);
        Assert.Equal("2.1 For Purpose 4.1 and 4.2", pointResult.Text![0].Text);
        Assert.Equal("", pointResult.Text![1].Text);
        Assert.Equal("Between National Grid References TL 55782 94571 and TL 55844 94741", pointResult.Text![2].Text);
        Assert.Equal("marked \"Point A\" and \"Point B\" on Map 1", pointResult.Text![3].Text); // TODO double quotes should be single
        Assert.Equal("", pointResult.Text![4].Text);
        Assert.Equal("2.2 For Purpose 4.3", pointResult.Text![5].Text);
        Assert.Equal("", pointResult.Text![6].Text);
        Assert.Equal("National Grid References", pointResult.Text![7].Text);
        Assert.Equal("From To", pointResult.Text![8].Text);
        Assert.Equal("TL5584494741 TL5453692523", pointResult.Text![9].Text);
        Assert.Equal("TL5502493346 TL5522093137", pointResult.Text![10].Text);
        
        Assert.Equal(2, pointResult.SubResults!.Count);

        var firstPoint = pointResult.SubResults![0];
        Assert.Equal("Point", firstPoint.MatchedLabel!.Name);
        
        Assert.Equal("2.1 For Purpose 4.1 and 4.2  Between National Grid References TL 55782 94571 and TL 55844 94741" 
                + " marked \"Point A\" and \"Point B\" on Map 1",
            string.Join(' ', firstPoint.Text?.Select(x => x.Text).ToArray()!));
        
        var firstPointSubs = firstPoint.SubResults;
        Assert.NotNull(firstPointSubs);
        Assert.Equal(3, firstPointSubs.Count);

        var firstPointPointNumber = firstPoint.SubResults![0];
        Assert.Equal("PointPointNumber", firstPointPointNumber.MatchedLabel!.Name);        
        Assert.Equal("2.1", firstPointPointNumber.Text![0].Text);
        
        var firstPointPurposeNumber = firstPoint.SubResults![1];
        Assert.Equal("4.1 and 4.2", firstPointPurposeNumber.Text![0].Text);

        Assert.NotNull(firstPointPurposeNumber.SubResults);
        Assert.Equal(2, firstPointPurposeNumber.SubResults.Count);
        
        Assert.Equal("4.1", firstPointPurposeNumber.SubResults[0].Text![0].Text);
        Assert.Equal("4.2", firstPointPurposeNumber.SubResults[1].Text![0].Text);
        
        var firstPointTextOnly = firstPoint.SubResults![2];
        Assert.Equal("TextWithoutPurposeAndPoint", firstPointTextOnly.MatchedLabel!.Name);
        Assert.Equal("Between National Grid References TL 55782 94571 and TL 55844 94741" 
                + " marked \"Point A\" and \"Point B\" on Map 1",
            string.Join(' ', firstPointTextOnly.Text?.Select(x => x.Text).ToArray()!));
        
        var secondPoint = pointResult.SubResults![1];
        var secondPointText = secondPoint.Text!;

        Assert.Equal(53, secondPointText.Count);
        Assert.Equal("2.2 For Purpose 4.3", secondPointText[0].Text);
        Assert.Equal(string.Empty, secondPointText[1].Text);
        Assert.Equal("National Grid References", secondPointText[2].Text);
        Assert.Equal("From To", secondPointText[3].Text);
        Assert.Equal("TL5584494741 TL5453692523", secondPointText[4].Text);
        
        // TODO update config to relate points to purposes
    }
    
    [Fact]
    public async Task LicenceToEA_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application Renewal Issued Licence- 25.01.2024.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Environment Agency", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("28/39/22/0390/R01", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(78, abstractionLimitsSection.Text?.Count);
        Assert.Equal(8, abstractionLimitsSection.SubResults!.Count);
        Assert.Equal(3, abstractionLimitsSection.SubResults[0].Text!.Count);        
        
        var point1 = abstractionLimitsSection.SubResults[0];
        Assert.Single(point1.SubResults!);
        Assert.Equal(3, point1.Text!.Count);
        
        var point1Sub1 = point1.SubResults![0];
        Assert.NotNull(point1Sub1.SubResults);
        Assert.Equal(4, point1Sub1.SubResults!.Count);

        var perDay = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(293, perDay.LineNumber);
        Assert.Equal("2500", perDay.Text?.FirstOrDefault()?.Text);
        
        var perDayUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        var perSecond = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("29", perSecond);
        
        var perSecondUnits = point1Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection2 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(3, abstractionLimitsSection2.Text!.Count);

        Assert.NotNull(abstractionLimitsSection2.SubResults);
        Assert.Single(abstractionLimitsSection2.SubResults!);

        var section2Sub1 = abstractionLimitsSection2.SubResults[0];
        Assert.Equal(4, section2Sub1.SubResults!.Count);
        
        perDay = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(296, perDay.LineNumber);
        Assert.Equal("5000", perDay.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        perSecond = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("57.9", perSecond);
        
        perSecondUnits = section2Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection3 = abstractionLimitsSection.SubResults[2];
        Assert.Equal(3, abstractionLimitsSection3.Text!.Count);

        Assert.NotNull(abstractionLimitsSection3.SubResults);
        Assert.Single(abstractionLimitsSection3.SubResults!);

        var section3Sub1 = abstractionLimitsSection3.SubResults[0];
        Assert.Equal(4, section3Sub1.SubResults!.Count);

        perDay = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(299, perDay.LineNumber);
        Assert.Equal("5000", perDay.Text?.FirstOrDefault()?.Text);
        
        perDayUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        perSecond = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("57.9", perSecond);
        
        perSecondUnits = section3Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        var abstractionLimitsSection4 = abstractionLimitsSection.SubResults[1];
        Assert.Equal(3, abstractionLimitsSection4.Text!.Count);

        Assert.NotNull(abstractionLimitsSection4.SubResults);
        Assert.Single(abstractionLimitsSection4.SubResults!);

        var section4Sub1 = abstractionLimitsSection4.SubResults[0];
        Assert.Equal(4, section4Sub1.SubResults!.Count);
        
        perDay = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel?.Format == "Number"
                && subResult.MatchedLabel.Text!.Any(text => text.Contains("per day")));

        Assert.NotNull(perDay);
        Assert.Equal(296, perDay.LineNumber);
        Assert.Equal("5000", perDay.Text?.FirstOrDefault()?.Text); // TODO there are 2 5000s and 1 5300
        
        perDayUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per day")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("cubic metres", perDayUnits);
        
        perSecond = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("57.9", perSecond); // TODO there is also 61.3
        
        perSecondUnits = section4Sub1.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);
        
        // TODO 4 more sections
    }
    
    [Fact]
    public async Task WhenNearNextLineIsCompany_NotCheckingAbstractionLimits_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application - Minor Variation  Issued licence -007-13122023.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Armstrongs Aggregates Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("NW/071/0309/007", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(37, abstractionLimitsSection.Text?.Count);
        
        Assert.Equal(4, abstractionLimitsSection.SubResults!.Count);
        var sectionPoint1 = abstractionLimitsSection.SubResults![0];

        Assert.Single(sectionPoint1.SubResults!);

        var sectionPoint1Sub1 = sectionPoint1.SubResults![0];
        Assert.Equal(9, sectionPoint1Sub1.SubResults!.Count);
        Assert.Single(sectionPoint1Sub1.SubResults[0].Text!); 
        
        // TODO expand this section + add others
    }
    
    [Fact]
    public async Task WhenNearPreviousLineIsCompany_SimpleAbstractionLimits1LiceneToLicenceLink_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application Minor Variation Issued Licence 11.12.2019 11149448.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Rolawn Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(14, abstractionLimitsSection.Text?.Count);
        Assert.Equal("The aggregate quantity of water authorised to be abstracted for the purpose of", 
            abstractionLimitsSection.Text![10].Text);
        Assert.Equal(2, abstractionLimitsSection.SubResults!.Count);
        Assert.Equal(9, abstractionLimitsSection.SubResults[0].Text!.Count);

        var point1 = abstractionLimitsSection.SubResults[0];
        var point1Sub1 = point1.SubResults![0];
        
        Assert.Equal("120", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("60000", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("33.3", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text!.First().Text);
        Assert.Equal("litres", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text!.First().Text);
        /*Assert.Equal("200000", subResult.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", subResult.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);*/
        
        // TODO

        /*Assert.Equal("NE/026/0034/052", abstractionLimitsResult.SubResults[1].SubResults![2].Text!.First().Text);
        Assert.Equal(5, abstractionLimitsResult.SubResults[1].Text!.Count);*/
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");   
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("NE/027/0028/059", licenceNumberResult.Text!.FirstOrDefault()?.Text);        
    }
    
    [Fact]
    public async Task XXXWhenSameLineIsCompany1Line_AndAbstractionLimitsToBeFoundWithSpellingMistake_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893476.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("J & S Accessories Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimits = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimits);
        Assert.False(abstractionLimits.IsOcr);
        Assert.Equal(11, abstractionLimits.Text?.Count);
        Assert.Equal("The aggregate quality of water authorised to be abstracted under this licence", abstractionLimits.Text![5].Text);
        Assert.Single(abstractionLimits.SubResults!);

        var abstractionLimitsPoint = abstractionLimits.SubResults![0];
        Assert.Equal(2, abstractionLimitsPoint.SubResults!.Count);
        
        var abstractionLimitPointSub1 = abstractionLimitsPoint.SubResults![0];
        
        Assert.Equal("20", abstractionLimitPointSub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", abstractionLimitPointSub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("475", abstractionLimitPointSub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", abstractionLimitPointSub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
            && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("173453", abstractionLimitPointSub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", abstractionLimitPointSub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
            && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);

        var abstractionLimitPointSub2 = abstractionLimitsPoint.SubResults![1];
        
        var linkedLicenceNumbers = abstractionLimitPointSub2.SubResults!
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
        
        var linkedLicences = abstractionLimitPointSub2.SubResults!
            .Where(subResult =>
                subResult.MatchedLabel!.Name == "LinkedLicence")
            .ToList();
        
        Assert.Equal(2, linkedLicences.Count);
        var linkedLicence1 = linkedLicences[0].SubResults;
        
        nameResult = linkedLicence1!.FirstOrDefault(result => result.LabelGroupName == "Company");

        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("J & S Accessories Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var licenceNumberResult = linkedLicence1!.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("25 68 001 247", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var linkedLicence2 = linkedLicences[1].SubResults;
        
        nameResult = linkedLicence2!.FirstOrDefault(result => result.LabelGroupName == "Company");

        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("J & S Accessories Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        licenceNumberResult = linkedLicence2!.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("25 68 001 248", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var linkedNameResult = linkedLicences[0].SubResults?.FirstOrDefault(result => result.LabelGroupName == "Company");
        Assert.Equal("J & S Accessories Limited", linkedNameResult?.Text?.FirstOrDefault()?.Text);
        
        var linkedLicenceNumber = linkedLicences[0].SubResults?.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        Assert.Equal("25 68 001 247", linkedLicenceNumber?.Text?.FirstOrDefault()?.Text);
        
        // TODO and the other licence
        licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("25 68 001 249", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task WhenSameLineIsCompany1Line_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application Vesting Licence Issued November 2017 011 10045454.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Philip John Hobbs", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(8, abstractionLimitsSection.Text?.Count);
        Assert.Single(abstractionLimitsSection.SubResults!);

        var sectionPoint1 = abstractionLimitsSection.SubResults![0];
        Assert.Single(sectionPoint1.SubResults!);
        
        var sectionPoint1Sub1 = sectionPoint1.SubResults![0];
        Assert.Equal(8, sectionPoint1Sub1.SubResults!.Count);

        Assert.Equal("32", sectionPoint1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("231", sectionPoint1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("4623", sectionPoint1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per month") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per month") == true)?.Text!.First().Text);
        Assert.Equal("13870", sectionPoint1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", sectionPoint1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);        
        Assert.Equal("16/51/007/S/011", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task WhenObscureCompanyName_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        const string filename = "Application NA New Issued Licence 11765926.pdf";
        
        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");     
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Chillingham Water Users", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel!.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(9, abstractionLimitsSection.Text?.Count);

        Assert.Single(abstractionLimitsSection.SubResults!);

        var abstractionLimitsPoint = abstractionLimitsSection.SubResults![0];
        Assert.Single(abstractionLimitsPoint.SubResults!);
        
        var point1Sub1 = abstractionLimitsPoint.SubResults![0];
        Assert.Equal(8, point1Sub1.SubResults!.Count);

        Assert.Equal("2", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("30", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("11000", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);
        Assert.Equal("0.6", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text!.First().Text);                
        Assert.Equal("litres", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text!.First().Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("NE/021/0000/036", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }

    [Fact]
    public async Task WhenPersonalNameNoTitle_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        const string filename = "Application - New - Issued Licence 31.01.2017 9655530.pdf";
        
        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Christopher Marler", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel!.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(9, abstractionLimitsSection.Text?.Count);
        Assert.Single(abstractionLimitsSection.SubResults!);

        var sectionPoint1 = abstractionLimitsSection.SubResults![0];
        Assert.Single(sectionPoint1.SubResults!);

        var point1Sub1 = sectionPoint1.SubResults![0];
        Assert.Equal(8, point1Sub1.SubResults!.Count);

        Assert.Equal("43.2", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text!.First().Text);                
        Assert.Equal("1037", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text!.First().Text);        
        Assert.Equal("37000", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);                
        Assert.Equal("cubic metres", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text!.First().Text);        
        Assert.Equal("12", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text!.First().Text);                
        Assert.Equal("litres", point1Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text!.First().Text);        
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("4/29/04/*S/0098/R01", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task WhenMultipleNamesWithNoTitle_And3ConditionsOfAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        const string filename = "Application Issued New Licence 2 23.2.2024.pdf";
        
        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Clemency Ives, Stephanie Williams, Octavia Williams",
            string.Join(", ", nameResult.Text!.Select(x => x.Text)));
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel!.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(28, abstractionLimitsSection.Text?.Count);

        Assert.Equal(3, abstractionLimitsSection.SubResults!.Count);

        var point1 = abstractionLimitsSection.SubResults![0];
        Assert.Single(point1.SubResults!);

        var point1Sub1 = point1.SubResults![0];
        Assert.Equal(8, point1Sub1.SubResults!.Count);
        Assert.Equal("90", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
            && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text!.First().Text);
        Assert.Equal("2160", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text![0].Text);   
        Assert.Equal("113650", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("25.3", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text![0].Text);
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text![0].Text);           

        // TODO add a test for the futher conditions 90,923
        
        /*Assert.Equal("SO/042/0036/023", subResult.SubResults[8].Text!.First().Text);
        Assert.Equal("110", subResult.SubResults[9].Text!.First().Text);
        Assert.Equal(6, subResult.SubResults[10].Text!.Count);
        Assert.Equal(6, subResult.SubResults[11].Text!.Count);
        Assert.Equal(14, subResult.SubResults[12].Text!.Count);*/
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("SO/042/0036/022", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task WhenCompanyNameBeforeLabelWhenUsuallyAfter_AndAbstractionLimitsToBeFound_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application New Licence July 2017 9867755.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");     
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Canterbury Golf Club Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(7, abstractionLimitsSection.Text?.Count);
        Assert.Single(abstractionLimitsSection.SubResults!);

        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults![0];
        Assert.Single(abstractionLimitsPoint1.SubResults!);

        var point1Sub1 = abstractionLimitsPoint1.SubResults![0];
        Assert.Equal(8, point1Sub1.SubResults!.Count);
        
        Assert.Equal("3.5", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("30", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("8300", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("0.97", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text![0].Text);
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text![0].Text);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("SO/040/0009/016", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task WhenX_EveyrhtingFoundButListSayingOtherwise_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application NA Formal Variation Licence 08122021.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");     
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("D.& M.Gedney Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.NearPreviousLineIsCompany, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(48, abstractionLimitsSection.Text?.Count);
        Assert.Equal(4, abstractionLimitsSection.SubResults!.Count);

        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults![0];
        Assert.Single(abstractionLimitsPoint1.SubResults!);

        var point1Sub1 = abstractionLimitsPoint1.SubResults![0];
        Assert.Equal(6, point1Sub1.SubResults!.Count);
        
        Assert.Equal("14", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("112", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("22731", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text![0].Text);
        
        // TODO, 3 other points
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("9/40/01/0500/G", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }
    
    [Fact]
    public async Task Z_Z_ThenFoundCorrectly()
    {
        // Arrange
        const string filename = "Application - formal variation - issue licence 9227047.pdf";
        
        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(6, resultList.Count);
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal("Public water supply", purposeResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["PURPOSE OF ABSTRACTION"], purposeResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchType.Between, purposeResult.MatchType);
        
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");     
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Thames Water Utilities Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var abstractionLimitsSection = resultList.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        
        Assert.NotNull(abstractionLimitsSection);
        Assert.False(abstractionLimitsSection.IsOcr);
        Assert.Equal(18, abstractionLimitsSection.Text?.Count);
        Assert.Equal(3, abstractionLimitsSection.SubResults!.Count);

        var abstractionLimitsPoint1 = abstractionLimitsSection.SubResults![0];
        Assert.Single(abstractionLimitsPoint1.SubResults!);

        var point1Sub1 = abstractionLimitsPoint1.SubResults![0];
        Assert.Equal(9, point1Sub1.SubResults!.Count);

        Assert.Equal("Up to and including 31 March 2025", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "DateOrPurpose"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("Up to and including ") == true)?.Text![0].Text);
        
        Assert.Equal("215", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per hour") == true)?.Text![0].Text);
        Assert.Equal("4550", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per day") == true)?.Text![0].Text);
        Assert.Equal("1460000", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("cubic metres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per year") == true)?.Text![0].Text);
        Assert.Equal("59.7", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Number"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text![0].Text);
        Assert.Equal("litres", point1Sub1.SubResults
            .FirstOrDefault(x => x.MatchedLabel!.Format == "Units"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("per second") == true)?.Text![0].Text);
        
        var abstractionLimitsPoint2 = abstractionLimitsSection.SubResults![1];
        Assert.Single(abstractionLimitsPoint2.SubResults!);
        
        var point2Sub1 = abstractionLimitsPoint2.SubResults![0];
        Assert.Equal(9, point2Sub1.SubResults!.Count);

        Assert.Equal("From 01 April 2025", point2Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "DateOrPurpose"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("From ") == true)?.Text![0].Text);
        
        var abstractionLimitsPoint3 = abstractionLimitsSection.SubResults![2];
        Assert.Single(abstractionLimitsPoint3.SubResults!);
        
        var point3Sub1 = abstractionLimitsPoint3.SubResults![0];
        Assert.Equal(8, point3Sub1.SubResults!.Count);

        Assert.Equal("The aggregate quantity of water authorised to be abstracted under this licence", // TODO " and under licence serial number 08/37/54/0061/R01 shall not exceed",
            point3Sub1.SubResults!
            .FirstOrDefault(x => x.MatchedLabel!.Format == "DateOrPurpose"
                && x.MatchedLabel.Text!.FirstOrDefault()?.Contains("aggregate quantity of water authorised") == true)?.Text![0].Text);                
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("08/37/54/0025", licenceNumberResult.Text!.FirstOrDefault()?.Text);
    }    
    
        [Fact]
    public async Task WhenABC_DEF_ThenY()
    {
        // Arrange
        const string filename = "06_transfer_application_new_licence_issued_2112018_10555534.pdf";

        // Act
        var resultList = await _pdfDataExtractor.GetMatchesAsync(
            PdfFolder + filename,
            LabelConfiguration.GetLabels(),
            FileLicenceMapping,
            [PdfFolder + filename],
            string.Empty,
            UseCache);
        
        // Assert
        Assert.Equal(5, resultList.Count);
        var nameResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Company");
        
        Assert.NotNull(nameResult);
        Assert.False(nameResult.IsOcr);
        Assert.Equal("Brett Aggregates Limited", nameResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["(\"the Licence Holder\")"], nameResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.LabelIsAfterTextToFind, nameResult.MatchedLabel.Position);
        Assert.Equal(MatchType.SameLineIsCompany1Line, nameResult.MatchType);
        
        var licenceNumberResult = resultList.FirstOrDefault(result => result.LabelGroupName == "LicenceNumber");        
        
        Assert.NotNull(licenceNumberResult);
        Assert.False(licenceNumberResult.IsOcr);
        Assert.Equal("TH/039/0028/051", licenceNumberResult.Text!.FirstOrDefault()?.Text);
        
        var meansOfAbstraction = resultList.FirstOrDefault(
            result => result.LabelGroupName == "MeansOfAbstraction");
        
        Assert.NotNull(meansOfAbstraction);
        Assert.False(meansOfAbstraction.IsOcr);
        Assert.Equal(1, meansOfAbstraction.Text?.Count);
        Assert.Equal(2, meansOfAbstraction.SubResults!.Count);
        
        var perSecond = meansOfAbstraction.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Number"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("86", perSecond);
        
        var perSecondUnits = meansOfAbstraction.SubResults
            .FirstOrDefault(subResult =>
                subResult.MatchedLabel!.Format == "Units"
                && subResult.MatchedLabel!.Text!.Any(text => text.Contains("per second")))?.Text?.FirstOrDefault()?.Text;
        Assert.Equal("litres", perSecondUnits);   
        
        var purposeResult = resultList.FirstOrDefault(result => result.LabelGroupName == "Purpose");    

        Assert.NotNull(purposeResult);
        Assert.False(purposeResult.IsOcr);
        Assert.Equal("Transfer for the purpose of dewatering", purposeResult.Text?.FirstOrDefault()?.Text);
        Assert.Equal(["PURPOSE OF ABSTRACTION"], purposeResult.MatchedLabel!.Text);
        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, purposeResult.MatchedLabel.Position);
        Assert.Equal(MatchType.Between, purposeResult.MatchType);
        
        Assert.Single(purposeResult.SubResults!);
        var firstPurpose = purposeResult.SubResults!.First();
        Assert.Equal("Transfer for the purpose of dewatering", firstPurpose.Text!.First().Text);
    }
}