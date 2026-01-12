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
            ("AnglianSplitLabels", GetAnglianSplitLabels()),
            ("AnglianWater", GetAnglianWaterLabels()),
            ("AnglianDivisions", GetAnglianDivisionsLabels()),
            ("LincolnWater", GetLincolnWaterLabels()),
            ("LincolnshireRiver", GetLincolnshireRiverLabels()),
            ("WellandNeneRiver", GetWellandNeneRiverLabels()),
            ("EastSuffolkNorfolkRiver", GetEastSuffolkNorfolkRiverLabels()),
            ("EssexRiver", GetEssexRiverLabels()),
            ("GreatOuseRiver", GetGreatOuseRiverLabels()),
            ("GenericRiverAuthority", GetGenericRiverAuthorityLabels()),
            ("NationalRivers", GetNationalRiversLabels()),
            ("YorkshireWater", GetYorkshireWaterLabels()),
            ("YorkshireRiver", GetYorkshireRiverLabels()),
            ("NorthumbrianWater", GetNorthumbrianWaterLabels()),
            ("GetNorthumbrianRiverLabels", GetNorthumbrianRiverLabels()),
            ("NRAModern1", GetNRAModern1Labels()),
            ("NRAModern2", GetNRAModern2Labels()),
            ("NRAAnglianModern1", GetNRAAnglianModern1Labels()),
            ("NRAAnglianModern2", GetNRAAnglianModern2Labels()),
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
    
    private static List<LabelToMatch> GetAnglianSplitLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "AnglianSplitLabels",
                Format = "Text",
                Text =
                [
                    new(".*Anglian.*Water.*Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*NORFOLK.*AND.*SUFFOLK.*RIVER.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*LINCOLN.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*LINCOLNSHIRE.*RIVER.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*NORWICH.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Great.*Ouse.*River.*Division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Colchester.*division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*N\\.R\\.A.*UNIT")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Cambridge.*Division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*WELLAND.*AND.*NENE.*RIVER.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Lincoln.*Water.*Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Lincolnshire.*River.*Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Welland.*and.*Nene.*River.*Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Oundle.*division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*East.*Suffolk.*and.*Norfolk.*River.*Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Essex.*River.*Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Great.*Ouse.*River.*Authority")
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
    
    private static List<LabelToMatch> GetAnglianWaterLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "AnglianWater",
                Format = "Text",
                Text =
                [
                    new(".*Anglian.*Water.*Authority")
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

    private static List<LabelToMatch> GetAnglianDivisionsLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "AnglianDivisions",
                Format = "Text",
                Text =
                [
                    new(".*NORFOLK.*AND.*SUFFOLK.*RIVER.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*LINCOLN.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*LINCOLNSHIRE.*RIVER.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*NORWICH.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Great.*Ouse.*River.*Division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Colchester.*division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*N\\.R\\.A.*UNIT")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Cambridge.*Division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*WELLAND.*AND.*NENE.*RIVER.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    },
                    new(".*Oundle.*division")
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

    private static List<LabelToMatch> GetLincolnWaterLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "LincolnWater",
                Format = "Text",
                Text =
                [
                    new(".*Lincoln.*Water.*Authority")
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

    private static List<LabelToMatch> GetLincolnshireRiverLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "LincolnshireRiver",
                Format = "Text",
                Text =
                [
                    new(".*Lincolnshire.*River.*Authority")
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

    private static List<LabelToMatch> GetWellandNeneRiverLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "WellandNeneRiver",
                Format = "Text",
                Text =
                [
                    new(".*Welland.*and.*Nene.*River.*Authority")
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

    private static List<LabelToMatch> GetEastSuffolkNorfolkRiverLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "EastSuffolkNorfolkRiver",
                Format = "Text",
                Text =
                [
                    new(".*East.*Suffolk.*and.*Norfolk.*River.*Authority")
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

    private static List<LabelToMatch> GetEssexRiverLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "EssexRiver",
                Format = "Text",
                Text =
                [
                    new(".*Essex.*River.*Authority")
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

    private static List<LabelToMatch> GetGreatOuseRiverLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "GreatOuseRiver",
                Format = "Text",
                Text =
                [
                    new(".*Great.*Ouse.*River.*Authority")
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

    private static List<LabelToMatch> GetGenericRiverAuthorityLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "GenericRiverAuthority",
                Format = "Text",
                Text =
                [
                    new("^River\\s+Authority$")
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

    private static List<LabelToMatch> GetNRAAnglianModern1Labels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Region",
                Format = "Text",
                Text =
                [
                    new(".*Anglian.* Region.*")
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
                    new(".*Licence.* Number.*")   {
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
    
    private static List<LabelToMatch> GetNRAAnglianModern2Labels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Region",
                Format = "Text",
                Text =
                [
                    new(".*Anglian.* Region.*")   
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
    
}