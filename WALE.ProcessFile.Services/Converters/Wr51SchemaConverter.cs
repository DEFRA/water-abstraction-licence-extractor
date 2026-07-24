using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Enums.Wr51;
using WALE.ProcessFile.Services.Models.OutputSchema.Wr51;

namespace WALE.ProcessFile.Services.Converters;

public static class Wr51SchemaConverter
{
    public static Wr51Form ToForm(MatchesResult matchesResult)
    {
        var rawFormDate = GetMultilineText(matchesResult, "Date");
        
        DateOnly? formDate = null;
        if (DateOnly.TryParse(rawFormDate, out var tFormDate))
        {
            formDate = tFormDate;
        }

        var rawInspectionDate = GetMultilineText(matchesResult, "InspectionDate");
        var rawInspectionTime = GetMultilineText(matchesResult, "Time");

        DateTime? inspectionDateTime = null;
        if (!string.IsNullOrWhiteSpace(rawInspectionDate))
        {
            var rawInspectionDateWithWordsRemove = rawInspectionDate.Replace("Time:", string.Empty);
            
            if (DateTime.TryParse($"{rawInspectionDateWithWordsRemove} {rawInspectionTime}", out var tIinspectionDateTime))
            {
                inspectionDateTime = tIinspectionDateTime;
            }
        }

        var rawDateOfCertificateOrRecord = GetMultilineText(matchesResult, "DateOfCertification");
        DateOnly? dateOfCertificateOrRecord = null;
        if (DateOnly.TryParse(rawDateOfCertificateOrRecord, out var tDateOfCertificateOrRecord))
        {
            dateOfCertificateOrRecord = tDateOfCertificateOrRecord;
        }

        var nameAndAddress = GetMultilineText(matchesResult, "NameAndAddress");
        var siteAddress = GetMultilineText(matchesResult, "SiteAddress");

        if (siteAddress?.Equals("Same as above", StringComparison.InvariantCultureIgnoreCase) == true
            || siteAddress?.Equals("As above", StringComparison.InvariantCultureIgnoreCase) == true)
        {
            siteAddress = nameAndAddress;
        }

        var whereKept = GetMultilineText(matchesResult, "WhereKept");
        if (string.IsNullOrWhiteSpace(whereKept)) whereKept = null;

        var documentTemplateVerison = GetMultilineText(matchesResult, "DocumentTemplateVersion");
        var isNewTemplate = documentTemplateVerison == "2026_07_10_v1";

        var documentHeader = GetMultilineText(matchesResult, "DocumentHeader");
        if (!string.IsNullOrWhiteSpace(documentHeader)) documentHeader = $"Form WR - {documentHeader}";
        
        string? maintenanceYesNo = null;

        if (isNewTemplate)
        {
            maintenanceYesNo = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineMaintenance");
        }
        else
        {
            var maintenanceYes = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineMaintenanceYes");
            var maintenanceNo = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineMaintenanceNo");

            if (maintenanceYes?.Equals("✓") == true)
            {
                maintenanceYesNo = "Yes";
            }
            else if (maintenanceNo?.Equals("✓") == true)
            {
                maintenanceYesNo = "No";
            }
        }

        string? readingsTakenYesNo = null;
        
        if (isNewTemplate)
        {
            readingsTakenYesNo = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineReadingsTaken");
        }
        else
        {
            var readingsTakenYes = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineReadingsTakenYes");
            var readingsTakenNo = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineReadingsTakenNo");

            if (readingsTakenYes?.Equals("✓") == true)
            {
                readingsTakenYesNo = "Yes";
            }
            else if (readingsTakenNo?.Equals("✓") == true)
            {
                readingsTakenYesNo = "No";
            }
        }

        var images = new List<string>();
        var pageIndex = 1;

        var filenameNoExtension = Path.GetFileNameWithoutExtension(matchesResult.Filename);
        
        foreach (var page in matchesResult.Pages)
        {
            for (var idx = 1; idx <= page.NumberOfImages; idx++)
            {
                if (pageIndex == 1 && idx == 1)
                {
                    // Skip EA Logo
                    continue;
                }
                
                var imageIndex = pageIndex == 1 && idx >= 2 ? idx - 1 : idx;
                images.Add($"{filenameNoExtension}/pdfpig-page{pageIndex}-image{imageIndex}.jpg");
            }

            pageIndex += 1;
        }
        
        return new Wr51Form
        {
            Metadata = new Wr51FormMetadata
            {
                DocumentTemplateVerison = documentTemplateVerison,
                DocumentHeader = documentHeader,
                Filename = matchesResult.Filename,
                FileId = matchesResult.FileId,
                IsScan = matchesResult.ScannedFile,
                FormSentTo = GetMultilineText(matchesResult, "FormSentTo"),
                Date = new Wr51FormInspectionDate
                {
                    RawDate = rawFormDate,
                    Date = formDate   
                }
            },
            LicenceNumber = GetMultilineText(matchesResult, "LicenceNumber"),
            InspectionClass = GetMultilineText(matchesResult, "InspectionClass"),
            Address = new Wr51FormAddress
            {
                NameAndAddress = nameAndAddress,
                TelephoneNumber = GetMultilineText(matchesResult, "TelephoneNumber"),
                SiteAddress = siteAddress      
            },
            MetWith = new Wr51FormMetWith
            {
                Name = GetMultilineText(matchesResult, "MetWith"),
                Position = GetMultilineText(matchesResult, "Position"),                
            },
            InspectingOfficer = GetMultilineText(matchesResult, "InspectingOfficer"),
            InspectionDate = new Wr51FormInspectionDateTime
            {
                DateTime = inspectionDateTime,
                RawDate = rawInspectionDate,
                RawTime = rawInspectionTime
            },
            LicenceProvisions = new Wr51FormLicenceProvisions
            {
                SourceOfSupply = GetInOrderStatus(matchesResult, "SourceOfSupply"),
                Purposes = GetInOrderStatus(matchesResult, "Purposes"),
                PointOfAbstraction = GetInOrderStatus(matchesResult, "PointOfAbstraction"),
                SpecialConditions = GetInOrderStatus(matchesResult, "SpecialConditions"),
                ChargingFactors = GetInOrderStatus(matchesResult, "ChargingFactors"),
                Land = GetInOrderStatus(matchesResult, "Land"),
                MeansOfAbstraction = GetInOrderStatus(matchesResult, "MeansOfAbstraction"),
                MeansOfMeasurement = GetInOrderStatus(matchesResult, "MeansOfMeasurement"),
                ProvisionOfInformation = GetInOrderStatus(matchesResult, "ProvisionOfInformation"),
                Quantities = GetInOrderStatus(matchesResult, "Quantities"),
                Records = GetInOrderStatus(matchesResult, "Records"),
                OtherProvisions = GetInOrderStatus(matchesResult, "OtherProvisions"),
                Period = GetInOrderStatus(matchesResult, "Period")
            },
            MeasurementDetails = new Wr51FormMeasurementDetails
            {
                MeterMake = GetMultilineText(matchesResult, "MeterMake"),
                SerialNumber = GetMultilineText(matchesResult, "SerialNumber"),
                Reading = GetMultilineText(matchesResult, "Reading"),
                Units = GetMultilineText(matchesResult, "Units"),
                Other = GetMultilineText(matchesResult, "Other"),
                CertificatesOrRecordsAvailableFor = GetMultilineText(matchesResult, "CertificatesOfRecords"),
                DateOfCertificateOrRecord = new Wr51FormInspectionDate
                {
                    Date = dateOfCertificateOrRecord,
                    RawDate = rawDateOfCertificateOrRecord
                },
                Calibration = GetMultilineText(matchesResult, "Calibration"),
                Conformance = GetMultilineText(matchesResult, "Conformance"),
                FlowVerification = GetMultilineText(matchesResult, "FlowVerification"),
                MeterVerification = GetMultilineText(matchesResult, "MeterVerification"),
                Maintenance = new Wr51FormMaintenance
                {
                    Maintenance = maintenanceYesNo,
                    Frequency = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineFrequency"),
                    ByWhom = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineByWhom")
                },
                ReadingsTaken = new Wr51FormReadingsTaken
                {
                    ReadingsTaken = readingsTakenYesNo,
                    Frequency = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineFrequency"),
                    ByWhom = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineByWhom")
                },
                WhereKept = whereKept
            },
            GeneralComments = GetMultilineText(matchesResult, "GeneralComments"),
            Images = images
        };
    }

    private static string? GetSingleLineSubFieldText(MatchesResult matchesResult, string name, string subName)
    {
        return matchesResult.Matches?
            .FirstOrDefault(m => m.MatchedLabelName == name)?
            .SubResults
            .FirstOrDefault(sr => sr.MatchedLabelName == subName)?
            .Text?
            .FirstOrDefault()?
            .Text;
    }
    
    private static string? GetMultilineText(MatchesResult matchesResult, string name)
    {
        var matchedLabel = matchesResult.Matches?
            .FirstOrDefault(m => m.MatchedLabelName == name);

        if (matchedLabel?.Text == null)
        {
            return null;
        }
        
        return string.Join("\n", matchedLabel.Text.Select(t => t.Text)!);
    }
    
    private static InOrderStatus GetInOrderStatus(MatchesResult matchesResult,  string name)
    {
        var labelGroupResult = matchesResult.Matches?
            .FirstOrDefault(m => m.MatchedLabelName == name);
        
        if (labelGroupResult == null)
        {
            return InOrderStatus.DidntMatch;
        }

        if (labelGroupResult.Text == null || labelGroupResult.Text.Count == 0)
        {
            return InOrderStatus.Blank;
        }

        var text = string.Join(" ", labelGroupResult.Text.Select(t => t.Text));

        if (string.IsNullOrWhiteSpace(text))
        {
            return InOrderStatus.Blank;
        }

        if (text.Equals("in", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("✓", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.InOrder;
        }
        
        if (text.Equals("not", StringComparison.InvariantCultureIgnoreCase)
            || text.Equals("X", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotInOrder;
        }
        
        if (text.Equals("n/a", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.NotApplicable;
        }
        
        return InOrderStatus.Unknown;
    }
}