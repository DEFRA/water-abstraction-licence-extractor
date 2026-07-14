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
            ("OtherProvisions", GetInOrderField("Other provisions (specify below):", "OtherProvisions"))
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