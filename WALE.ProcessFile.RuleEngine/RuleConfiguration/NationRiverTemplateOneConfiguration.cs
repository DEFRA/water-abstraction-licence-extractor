using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums;

namespace WALE.ProcessFile.RuleEngine.RuleConfiguration;

public static class NationalRiverTemplateOneConfiguration
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
                    new(".*National.* River.* Authority")
                    {
                        IsRegularExpression = true
                    },
                    new("Water Resources Act 1991")
                    {
                        IsRegularExpression = true
                    },
                    new("Point of abstraction")
                    {
                        IsRegularExpression = true
                    },
                    new("Purpose of abstraction")
                    {
                        IsRegularExpression = true
                    },
                    new("Land on which licence authorises use of water")
                    {
                        IsRegularExpression = true
                    },
                    new("Period of abstraction")
                    {
                        IsRegularExpression = true
                    },
                    new("Maximum quantity of water to be extracted during the specified period")
                    {
                        IsRegularExpression = true
                    },
                    new("Means of measurement of water abstracted")
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
            new LabelToMatch
            {
                Name = "Variation",
                Format = "Text",
                Text = 
                [
                    new("Variation"),
                    new("Superseded"),
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ApplicableToMost,
                IncludeStartLabelText = true
            }
        ];
    }
}