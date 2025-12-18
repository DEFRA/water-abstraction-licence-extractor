using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums;

namespace WALE.ProcessFile.RuleEngine.RuleConfiguration;

public static class EAScannedTemplateFourConfiguration
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
                    new("Licence Certificate"),
                    new("Licence to abstract water"),
                    new("Environment Act 1995"),
                    new("Water Resources Act 1991"),
                    new("Water Resources (Succession to Licences) Regulations 1969"),
                    new("Water Resources (Licences) Regulations 1965"),
                    new("Ordinary Licence Conditions")
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