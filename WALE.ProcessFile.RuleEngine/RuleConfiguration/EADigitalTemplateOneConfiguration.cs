using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums;

namespace WALE.ProcessFile.RuleEngine.RuleConfiguration;

public static class EADigitalTemplateOneConfiguration
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
                    new("Water Resources Licence To Abstract Water")
                    {
                        IsRegularExpression = true
                    },
                    new("Full Licence to abstract water ")
                    {
                        IsRegularExpression = true
                    },
                    new("Environment Act 1995")
                    {
                        IsRegularExpression = true
                    },
                    new("Water Resources Act 1991 as amended by the Water Act 2003")
                    {
                        IsRegularExpression = true
                    },
                    new("Water Resources (Abstraction and Impounding) Regulations 2006")
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