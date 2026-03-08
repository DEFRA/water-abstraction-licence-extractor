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
            ("NESplitLabels", GetSplitLabels()),
            ("AnglianSplitLabels", GetAnglianSplitLabels()),
            ("NWSplitLabels", GetNWSplitLabels()),
            ("NationalRivers", GetNationalRiversLabels()),
            ("NENRAModern1", GetNRAModern1Labels()),
            ("NENRAModern2", GetNRAModern2Labels()),
            ("AnglianNRAModern1", GetNRAAnglianModern1Labels()),
            ("NWNRAModern1", GetNRANWModern1Labels()),
            ("AnglianNRAModern2", GetNRAAnglianModern2Labels()),
            ("NRAOld", GetNRAOldLabels()),
            ("NWNRAOld", GetNRANWOldLabels()),
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Name = "Yorkshire Water Authority",
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Yorkshire River Authority",
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Northumbrian Water Authority",
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Northumbrian River Authority",
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Name = "Anglian Water Authority",
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Nofolk And Suffolk River Division",
                Format = "Text",
                Text =
                [
                    new(".*NORFOLK.*AND.*SUFFOLK.*RIVER.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "lincoln Division",
                Format = "Text",
                Text =
                [
                    new(".*LINCOLN DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Lincolnshire River Division",
                Format = "Text",
                Text =
                [
                    new(".*LINCOLNSHIRE.*RIVER.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Norwich Division",
                Format = "Text",
                Text =
                [
                    new(".*NORWICH.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Great Ouse River Division",
                Format = "Text",
                Text =
                [
                    new(".*Great.*Ouse.*River.*Division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Colchester Division",
                Format = "Text",
                Text =
                [
                    new(".*Colchester.*division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "NRA Unit",
                Format = "Text",
                Text =
                [
                    new(".*N\\.R\\.A.*UNIT")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Cambridge Division",
                Format = "Text",
                Text =
                [
                    new(".*Cambridge.*Division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Welland And Nene River Division",
                Format = "Text",
                Text =
                [
                    new(".*WELLAND.*AND.*NENE.*RIVER.*DIVISION")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Lincoln Water Authority",
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Lincolnshire River Authority",
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Welland and Nene River Authority",
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Oundle Division",
                Format = "Text",
                Text =
                [
                    new(".*Oundle.*division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "East Suffolk and Norfolk River Authority",
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Essex River Authority",
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Great Ouse River Authority",
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            }
        ];
    }

    private static List<LabelToMatch> GetNWSplitLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Mersey and Weaver River Authority",
                Format = "Text",
                Text =
                [
                    new(".*Mersey .*and .*Weaver .*River .*Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Lancaster River Authority",
                Format = "Text",
                Text =
                [
                    new(".*Lancaster .*River .*Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "North West Water Authority - River Division",
                Format = "Text",
                Text =
                [
                    new(".*North .*West .*Water .*Authority.* -.*River .*Division")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            },
            new LabelToMatch
            {
                Name = "Cumberland River Authority",
                Format = "Text",
                Text =
                [
                    new(".*Cumberland .*River .*Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            }
        ];
    }
    
    private static List<LabelToMatch> GetNRANWModern1Labels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Region",
                Format = "Text",
                Text =
                [
                    new(".*North.* West.* Region.*")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            }
        ];
    }
    
    
    private static List<LabelToMatch> GetNRANWOldLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Region",
                Format = "Text",
                Text =
                [
                    new(".*National.* .*Rivers.* Authority")
                    {
                        IsRegularExpression = true,
                        RegularExpressionIsCaseInsensitive = true
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
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
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            }
        ];
    }
}