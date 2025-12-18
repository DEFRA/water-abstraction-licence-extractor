using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums;

namespace WALE.ProcessFile.RuleEngine.RuleConfiguration;

public static class EAScannedTemplateTwoConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("Included", GetIncludedLabels()),
            ("Excluded", GetExcludedLabels()),
            ("Variation", GetVariationLabels()),
        ];
    }
    private static List<LabelToMatch> GetIncludedLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Included",
                Format = "Text",
                Text =
                [
                    new("Environment.* Agency")
                    {
                        IsRegularExpression = true
                    },
                    new("Licence to abstract water")
                    {
                        IsRegularExpression = true
                    },
                    new("Licence holders and successors")
                    {
                        IsRegularExpression = true
                    },
                    new("Means of (measurement) (assessment) of water abstracted")
                    {
                        IsRegularExpression = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ActuallyLabel,
                IncludeStartLabelText = true
            }
        ];
    }
    
    private static List<LabelToMatch> GetExcludedLabels()
    {
        return
        [
        ];
    }
    
    private static List<LabelToMatch> GetVariationLabels()
    {
        return
        [
            
        ];
    }
}