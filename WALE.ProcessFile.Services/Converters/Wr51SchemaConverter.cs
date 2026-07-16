using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Enums.Wr51;
using WALE.ProcessFile.Services.Models.OutputSchema.Wr51;

namespace WALE.ProcessFile.Services.Converters;

public static class Wr51SchemaConverter
{
    public static async Task<Wr51Form> ToFormAsync(MatchesResult matchesResult)
    {
        return new Wr51Form
        {
            Metadata = new Wr51FormMetadata
            {
                DocumentTemplateVerison = GetMultilineText(matchesResult, "DocumentTemplateVersion"),
                Filename = matchesResult.Filename,
                IsScan = matchesResult.ScannedFile
            },
            LicenceNumber = GetMultilineText(matchesResult, "LicenceNumber"),
            InspectionClass = GetMultilineText(matchesResult, "InspectionClass"),
            NameAndAddress = GetMultilineText(matchesResult, "NameAndAddress"),
            TelephoneNumber = GetMultilineText(matchesResult, "TelephoneNumber"),
            SiteAddress = GetMultilineText(matchesResult, "SiteAddress"),
            MetWith = GetMultilineText(matchesResult, "MetWith"),
            MetWithsPosition = GetMultilineText(matchesResult, "Position"),
            InspectingOfficer = GetMultilineText(matchesResult, "InspectingOfficer"),
            InspectionDate = GetMultilineText(matchesResult, "InspectionDate"),
            InspectionTime = GetMultilineText(matchesResult, "Time"),
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
            Period = GetInOrderStatus(matchesResult, "Period"),
            MeterMake = GetMultilineText(matchesResult, "MeterMake"),
            SerialNumber = GetMultilineText(matchesResult, "SerialNumber"),
            Reading = GetMultilineText(matchesResult, "Reading"),
            Units = GetMultilineText(matchesResult, "Units"),
            Other = GetMultilineText(matchesResult, "Other"),
            CertificatesOrRecordsAvailableFor = GetMultilineText(matchesResult, "CertificatesOfRecords"),
            DateOfCertificateOrRecord = GetMultilineText(matchesResult, "DateOfCertification"),
            Calibration = GetMultilineText(matchesResult, "Calibration"),
            Conformance = GetMultilineText(matchesResult, "Conformance"),
            FlowVerification = GetMultilineText(matchesResult, "FlowVerification"),
            MeterVerification = GetMultilineText(matchesResult, "MeterVerification"),
            Maintenance = new Wr51FormMaintenance
            {
                Maintenance = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineMaintenance"),
                Frequency = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineFrequency"),
                ByWhom = GetSingleLineSubFieldText(matchesResult, "MaintenanceLine", "MaintenanceLineByWhom")
            },
            ReadingsTaken = new Wr51FormReadingsTaken
            {
                ReadingsTaken = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineReadingsTaken"),
                Frequency = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineFrequency"),
                ByWhom = GetSingleLineSubFieldText(matchesResult, "ReadingsTakenLine", "ReadingsTakenLineByWhom")
            },
            WhereKept = GetMultilineText(matchesResult, "WhereKept"),
            GeneralComments = GetMultilineText(matchesResult, "GeneralComments"),
            FormSentTo = GetMultilineText(matchesResult, "FormSentTo"),
            Date = GetMultilineText(matchesResult, "Date")
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

        if (text.Equals("in", StringComparison.InvariantCultureIgnoreCase))
        {
            return InOrderStatus.InOrder;
        }
        
        if (text.Equals("not", StringComparison.InvariantCultureIgnoreCase))
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