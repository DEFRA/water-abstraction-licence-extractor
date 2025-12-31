using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums;

namespace WALE.ProcessFile.RuleEngine.RuleConfiguration;

public static class TemplateFinderRuleConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("EALabel", GetEALabels()),
            ("SplitLabels", GetSplitLabels()),
            ("NationalRivers", GetNationalRiversLabels()),
            ("YorkshireWater", GetYorkshireWaterLabels()),
            ("YorkshireRiver", GetYorkshireRiverLabels()),
            ("NorthumbrianWater", GetNorthumbrianWaterLabels()),
            ("GetNorthumbrianRiverLabels", GetNorthumbrianRiverLabels()),
        ];
    }
    private static List<LabelToMatch> GetEALabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "EALabel",
                Format = "Text",
                Text =
                [
                    new("Environment.* Agency")
                    {
                        IsRegularExpression = true
                    }    ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ActuallyLabel,
                IncludeStartLabelText = true
            }
        ];
    }
    
    private static List<LabelToMatch> GetYorkshireWaterLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "YorkshireWater",
                Format = "Text",
                Text =
                [
                    new(".*Yorkshire.* Water Authority")
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
    
    private static List<LabelToMatch> GetYorkshireRiverLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "YorkshireRiver",
                Format = "Text",
                Text =
                [
                    new(".*Yorkshire.* River.* Authority")
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
    private static List<LabelToMatch> GetNorthumbrianWaterLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "NorthumbrianWater",
                Format = "Text",
                Text =
                [
                    new(".*Northumbrian.* Water.* Authority")
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
    
    private static List<LabelToMatch> GetNorthumbrianRiverLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "NorthumbrianRiverLabels",
                Format = "Text",
                Text =
                [
                    new(".*Northumbrian.* River.* Authority")
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
    
    private static List<LabelToMatch> GetSplitLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "SplitLabels",
                Format = "Text",
                Text =
                [
                    new(".*Yorkshire.* Water Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*Yorkshire.* River.* Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*Northumbrian.* Water.* Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*Northumbrian.* River.* Authority")
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
    
    private static List<LabelToMatch> GetNationalRiversLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "NationalRivers",
                Format = "Text",
                Text =
                [
                    new(".*National.* River.* Authority")
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
}