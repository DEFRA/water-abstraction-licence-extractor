using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Configuration;

public static class Wr51LabelConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("SourceOfSupply", GetInOrderField("Source of supply:", "SourceOfSupply")),
            ("PointOfAbstraction", GetInOrderField("Point of abstraction:", "PointOfAbstraction")),
            ("MeansOfAbstraction", GetInOrderField("Means of abstraction:", "MeansOfAbstraction")),
            ("Purposes", GetInOrderField("Purpose(s):", "Purposes")),
            ("Period", GetInOrderField("Period:", "Period")),
            ("Quantities", GetInOrderField("Quantities:", "Quantities")),
            ("MeansOfMeasurement", GetInOrderField("Means of measurement:", "MeansOfMeasurement")),
            ("Records", GetInOrderField("R ecords:", "Records")),
            ("ProvisionOfInformation", GetInOrderField("Provision of information:", "ProvisionOfInformation")),
            ("SpecialConditions", GetInOrderField("Special conditions:", "SpecialConditions")),
            ("Land", GetInOrderField("Land (only if specified):", "Land")),
            ("ChargingFactors", GetInOrderField("Charging factors:", "ChargingFactors")),
            ("OtherProvisions", GetInOrderField("Other provisions (specify below):", "OtherProvisions")),
            ("LicenceNumber", TextAfterLabel("Licence No. (or Application No. or GIC No. etc.)", "LicenceNumber", 1)),
            ("MetWith", TextAfterLabel("Met with:", "MetWith", 0)),
            ("InspectingOfficer", TextAfterLabel("Inspecting Officer:", "InspectingOfficer", 0)),
            ("SiteAddress", TextAfterLabel("Site address (if different):", "SiteAddress", 1)),
            ("InspectionClass", TextAfterLabel("Inspection Class:", "InspectionClass", 1)),
            ("TelephoneNumber", TextAfterLabel("Telephone No:", "TelephoneNumber", 1)),
            ("Position", TextAfterLabel("Position:", "Position", 0)),
            ("InspectionDate", TextAfterLabel("Inspection Date:", "InspectionDate", 1)),
            ("Time", TextAfterLabel("Time:", "Time", 0)),
            ("NameAndAddress", TextAfterLabel("Name and address:", "NameAndAddress", 2)),
        ];
    }
    
    private static List<LabelToMatch> TextAfterLabel(string text, string labelName, int nextLinesToFetch)
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
                    new(text)
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
                    new(text) { ColumnMustStartWith = true }
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                LimitTo = LimitTo.SameColumn,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1,
                Name = labelName,
                Possibilities = [
                    new TextToMatch("N/A"),
                    new TextToMatch("Not"),
                    new TextToMatch("In")
                ]
            }
        ];
    }
}