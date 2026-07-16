using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Configuration;

public static class Wr51LabelConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("SourceOfSupply", GetInOrderField("Source of supply", "SourceOfSupply")),
            ("PointOfAbstraction", GetInOrderField("Point of abstraction", "PointOfAbstraction")),
            ("MeansOfAbstraction", GetInOrderField("Means of abstraction", "MeansOfAbstraction")),
            ("Purposes", GetInOrderField("Purpose(s)", "Purposes")),
            ("Period", GetInOrderField("Period", "Period")),
            ("Quantities", GetInOrderField("Quantities", "Quantities")),
            ("MeansOfMeasurement", GetInOrderField("Means of measurement", "MeansOfMeasurement")),
            ("Records", GetInOrderField("R ecords", "Records")),
            ("ProvisionOfInformation", GetInOrderField("Provision of information", "ProvisionOfInformation")),
            ("SpecialConditions", GetInOrderField("Special conditions", "SpecialConditions")),
            ("Land", GetInOrderField("Land (only if specified)", "Land")),
            ("ChargingFactors", GetInOrderField("Charging factors", "ChargingFactors")),
            ("OtherProvisions", GetInOrderField("Other provisions (specify below)", "OtherProvisions")),
            ("LicenceNumber", TextAfterLabel("Licence No. (or Application No. or GIC No. etc.)", "LicenceNumber", 1)),
            ("MetWith", TextAfterLabel("Met with", "MetWith", 0)),
            ("InspectingOfficer", TextAfterLabel("Inspecting Officer", "InspectingOfficer", 0)),
            ("SiteAddress", TextToFindIsBetweenLabels("Site address (if different)", "Met with", "SiteAddress", 1, LimitTo.SameColumn)),
            ("InspectionClass", TextToFindIsBetweenLabels("Inspection Class", "Telephone No", "InspectionClass", 1, LimitTo.SameColumn)),
            ("TelephoneNumber", TextToFindIsBetweenLabels("Telephone No", "Email", "TelephoneNumber", 2, LimitTo.SameColumn)),
            ("Position", TextToFindIsBetweenLabels("Position", "Inspection Date", "Position", 1, LimitTo.SameColumn)),
            ("Time", TextAfterLabel("Time", "Time", 0)),
            ("NameAndAddress", TextToFindIsBetweenLabels("Name and address", "Site address", "NameAndAddress", 7, LimitTo.SameColumn)),
            ("MeterMake", TextToFindIsBetweenLabels("Meter make", "Reading:", "MeterMake", 1, LimitTo.SameColumn)),
            ("SerialNumber", TextAfterLabel("Serial number", "SerialNumber", 0)),
            ("Reading", TextAfterLabel("Reading", "Reading", 0)),
            ("Units", TextAfterLabel("Units", "Units", 0)),
            ("Other", TextAfterLabel("Other:", "Other", 0)),
            ("CertificatesOfRecords", TextAfterLabel("Certificates or records available for", "CertificatesOfRecords", 0)),
            ("DateOfCertification", TextAfterLabel("Date of certificate or", "DateOfCertification", 1, [new("record:")])),
            ("Calibration", TextAfterLabel("Calibration", "Calibration", 1)),
            ("Conformance", TextAfterLabel("Conformance", "Conformance", 1)),
            ("FlowVerification", TextAfterLabel("Flow verification", "FlowVerification", 1)),
            ("MeterVerification", TextAfterLabel("Meter verification", "MeterVerification", 1)),
            ("WhereKept", TextAfterLabel("Where kept", "WhereKept", 0)),
            ("FormSentTo", TextAfterLabel("Form sent to", "FormSentTo", 1)),
            ("Date", TextAfterLabel("Date:", "Date", 0)),
            ("DocumentTemplateVersion", TextAfterLabel("Document Template Version:", "DocumentTemplateVersion", 0)),
            ("GeneralComments", TextToFindIsBetweenLabels(
                "General comments, details / dates of occupation changes, actions required etc.",
                "Form sent to",
                "GeneralComments",
                100,
                LimitTo.WholeLine)),
            ("MaintenanceLine", MaintenanceLine("Maintenance:", "Readings taken", "MaintenanceLine")),
            ("ReadingsTakenLine", MaintenanceLine("Readings taken:", "Where Kept", "ReadingsTakenLine")),
            ("InspectionDate", TextAfterLabelWithSpecifiedColumn("Inspection Date:", "InspectionDate", 2, 1)),
            ("Email", TextToFindIsBetweenLabels("Email", "Position:", "Email", 1, LimitTo.SameColumn)),
        ];
    }
    
    private static List<LabelToMatch> MaintenanceLine(string textStart, string textEnd, string name)
    {
        return
        [
            new LabelToMatch
            {
                TextStart =
                [
                    new(textStart)
                    {
                        LineMustStartWith = true
                    }
                ],
                TextEnd = 
                [
                    new(textEnd) { LineMustStartWith = true},
                    new("[END_OF_BLOCK]")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1,
                Name = name,
                IncludeStartLabelText = true,
                SubLabels =
                [
                    name == "MaintenanceLine"
                        ? TextAfterLabel("Maintenance:", $"{name}Maintenance", 0)[0]
                        : TextAfterLabel("Readings taken:", $"{name}ReadingsTaken", 0)[0],
                    TextAfterLabel("Frequency:", $"{name}Frequency", 0)[0],
                    TextAfterLabel("By whom:", $"{name}ByWhom", 0)[0]
                ]
            }
        ];
    }
    
    private static List<LabelToMatch> TextToFindIsBetweenLabels(
        string startText,
        string endText,
        string name,
        int nextLines,
        LimitTo limitTo)
    {
        return
        [
            new LabelToMatch
            {
                TextStart =
                [
                    new(startText)
                    {
                        ColumnMustStartWith = true
                    }
                ],
                TextEnd = 
                [
                    new(endText) { LineMustStartWith = true},
                    new("[END_OF_BLOCK]")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = nextLines,
                LimitTo = limitTo,
                Name = name,
                Remove = [
                    new(startText) // TODO not sure why we have to add this, we dont always have to with betweens - probably because of the column limiting
                ]
            }
        ];
    }
    
    private static List<LabelToMatch> TextAfterLabelWithSpecifiedColumn(
        string text,
        string labelName,
        int nextLinesToFetch,
        int columnIndex)
    {
        var label = TextAfterLabel(text, labelName, nextLinesToFetch, []);
        label[0].LimitTo = LimitTo.SpecifiedColumn;
        label[0].LimitToColumnIndex = columnIndex;
        
        return label;
    }
    
    private static List<LabelToMatch> TextAfterLabel(
        string text,
        string labelName,
        int nextLinesToFetch)
    {
        return TextAfterLabel(text, labelName, nextLinesToFetch, []);
    }
    
    private static List<LabelToMatch> TextAfterLabel(
        string text,
        string labelName,
        int nextLinesToFetch,
        List<TextToMatch> additionalRemoves)
    {
        return
        [
            new LabelToMatch
            {
                Text =
                [
                    new(text)
                    {
                        ColumnMustStartWith = true
                    }
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                LimitTo = LimitTo.SameColumn,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = nextLinesToFetch,
                Name = labelName,
                Remove = [
                    new(text),
                    ..additionalRemoves
                ]
            }
        ];
    }
    
    private static List<LabelToMatch> GetInOrderField(string text, string labelName)
    {
        return
        [
            new LabelToMatch
            {
                Text =
                [
                    new(text) { ColumnMustStartWith = true },
                    new(text.Replace(" ", string.Empty)) { ColumnMustStartWith = true }
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                LimitTo = LimitTo.SameColumn,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1,
                Name = labelName,
                Remove = [
                    new(text) // Gets rid of issue of finding 'in' in 'Points'
                ],
                Possibilities = [
                    new TextToMatch("N/A"),
                    new TextToMatch("Not"),
                    new TextToMatch("In"),
                    new TextToMatch("✓"),
                    new TextToMatch("X")
                ]
            }
        ];
    }
}