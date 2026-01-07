using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

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
            ("NRAModern1", GetNRAModern1Labels()),
            ("NRAModern2", GetNRAModern2Labels()),
            ("NRAOld", GetNRAOldLabels()),
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
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }   
                ],
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
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
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
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
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
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
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
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
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
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Yorkshire.* River.* Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Northumbrian.* Water.* Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Northumbrian.* River.* Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
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
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }    
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ActuallyLabel,
                IncludeStartLabelText = true
            }
        ];
    }
    
    private static List<LabelToMatch> GetNRAModern1Labels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Region",
                Format = "Text",
                Text =
                [
                    new(".*Northumbria.* Yorkshire.* Region.*")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ActuallyLabel,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Licence",
                Format = "Text",
                Text =
                [
                    new(".*Licence.* Serial.*")   {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ActuallyLabel,
                IncludeStartLabelText = true
            }
        ];
    }
    
    private static List<LabelToMatch> GetNRAModern2Labels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Region",
                Format = "Text",
                Text =
                [
                    new(".*Yorkshire.* Region.*")   
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ActuallyLabel,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Licence",
                Format = "Text",
                Text =
                [
                    new(".*Serial.* .*No.*")   {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ActuallyLabel,
                IncludeStartLabelText = true
            }
        ];
    }
    
    private static List<LabelToMatch> GetNRAOldLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Region",
                Format = "Text",
                Text =
                [
                    new(".*Northumbria.* .*Region.*")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ActuallyLabel,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Licence",
                Format = "Text",
                Text =
                [
                    new(".*Licence.* .*No.*")   
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
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