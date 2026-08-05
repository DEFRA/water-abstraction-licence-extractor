using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WRADI.DocumentType.AbstractionLicence.Configuration;

public static partial class LicenceReaderConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("LicenceNumber", SharedLabels.GetLicenceNumberLabels()),
            ("DateOfIssue", SharedLabels.GetDateOfIssueLabels()),
            
            ("Licence Header", GetHeaderLabels()),
            ("Addendum", GetAddendumLabels()),
            
            ("EALabel", GetEaLabels()),
            ("NESplitLabels", GetNeSplitLabels()),
            ("AnglianSplitLabels", GetAnglianSplitLabels()),
            ("NWSplitLabels", GetNWSplitLabels()),
            ("NationalRivers", GetNationalRiversLabels()),
            ("NENRAModern1", GetNRAModern1Labels()),
            ("NENRAModern2", GetNRAModern2Labels()),
            ("AnglianNRAModern1", GetNRAAnglianModern1Labels()),
            ("NWNRAModern1", GetNRANWModern1Labels()),
            ("AnglianNRAModern2", GetNRAAnglianModern2Labels()),
            ("NRAOld", GetNRAOldLabels()),
            ("NWNRAOld", GetNRANWOldLabels())
        ];
    }
    
    private static List<LabelToMatch> GetHeaderLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Licence Header",
                Format = "Text",
                TextStart =
                [
                    new("SCHEDULE OF CONDITIONS"),
                    new("Licence of right to abstract water"),
                    new("Licence [of right] to abstract water"),
                    new("Licence to abstract water")
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ApplicableToMost,
                IncludeStartLabelText = true
            }
        ];
    }
    
    private static List<LabelToMatch> GetAddendumLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Addendum",
                Format = "Text",
                Text = 
                [
                    new("Please keep this addendum with")
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ApplicableToMost,
                IncludeStartLabelText = true
            }
        ];
    }
    
    private static List<LabelToMatch> GetEaLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "EALabel",
                Format = "Text",
                Text =
                [
                    new(string.Empty)
                    {
                        Regex = EnvironmentAgencyRegex()
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            }
        ];
    }
    
    private static List<LabelToMatch> GetNeSplitLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Yorkshire Water Authority",
                Format = "Text",
                Text =
                [
                    new(string.Empty)
                    {
                        Regex = YorkshireWaterAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = YorkshireRiverAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = NorthumbrianWaterAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = NorthumbrianRiverAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = NationalRiverAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = NorthumbriaYorkshireRegionRegex()
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
                    new(string.Empty)
                    {
                        Regex = LicenceSerialRegex()
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
                    new(string.Empty)
                    {
                        Regex = YorkshireRegionRegex()
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
                    new(string.Empty)
                    {
                        Regex = SerialNoRegex()
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
                    new(string.Empty)
                    {
                        Regex = NorthumbriaRegionRegex()
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
                    new(string.Empty)
                    {
                        Regex = LicenceNoRegex()
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
                    new(string.Empty)
                    {
                        Regex = AnglianWaterAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = NofolkAndSuffolkRiverDivisionRegex()
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
                    new(string.Empty)
                    {
                        Regex = LincolnDivisionRegex()
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
                    new(string.Empty)
                    {
                        Regex = LincolnshireRiverDivisionRegex()
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
                    new(string.Empty)
                    {
                        Regex = NorwichDivisionRegex()
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
                    new(string.Empty)
                    {
                        Regex = GreatOuseRiverDivisionRegex()
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
                    new(string.Empty)
                    {
                        Regex = ColchesterDivisionRegex()
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
                    new(string.Empty)
                    {
                        Regex = NraUnitRegex()
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
                    new(string.Empty)
                    {
                        Regex = CambridgeDivisionRegex()
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
                    new(string.Empty)
                    {
                        Regex = WellandAndNeneRiverDivisionRegex()
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
                    new(string.Empty)
                    {
                        Regex = LincolnWaterAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = LincolnshireRiverAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = WellandAndNeneRiverAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = OundleDivisionRegex()
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
                    new(string.Empty)
                    {
                        Regex = EastSuffolkAndNorfolkRiverAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = EssexRiverAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = GreatOuseRiverAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = AnglianRegionRegex()
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
                    new(string.Empty)
                    {
                        Regex = LicenceNumberRegex()
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
                        Regex = AnglianRegionRegex()
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
                    new(".*Serial.* .*No.*")
                    {
                        Regex = SerialNoRegex()
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
                    new(string.Empty)
                    {
                        Regex = MerseyAndWeaverRiverAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = LancasterRiverAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = NorthWestWaterAuthorityRiverDivisionRegex()
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
                    new(string.Empty)
                    {
                        Regex = CumberlandRiverAuthorityRegex()
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
                    new(string.Empty)
                    {
                        Regex = NorthWestRegionRegex()
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
                    new(".*Licence.* Number.*")
                    {
                        Regex = LicenceNumberRegex()
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
                    new(string.Empty)
                    {
                        Regex = NationalRiversAuthorityRegex()
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
                        Regex = LicenceNoRegex()
                    }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.LabelIsActuallyResult,
                IncludeStartLabelText = true
            }
        ];
    }
    
    [GeneratedRegex("Environment.* Agency", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex EnvironmentAgencyRegex();
    
    [GeneratedRegex(".*Yorkshire.* Water Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex YorkshireWaterAuthorityRegex();
    
    [GeneratedRegex(".*Yorkshire.* River.* Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex YorkshireRiverAuthorityRegex();
    
    [GeneratedRegex(".*Northumbrian.* Water.* Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex NorthumbrianWaterAuthorityRegex();
    
    [GeneratedRegex(".*Northumbrian.* River.* Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex NorthumbrianRiverAuthorityRegex();
    
    [GeneratedRegex(".*National.* River.* Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex NationalRiverAuthorityRegex();
    
    [GeneratedRegex(".*Northumbria.* Yorkshire.* Region.*", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex NorthumbriaYorkshireRegionRegex();
    
    [GeneratedRegex(".*Licence.* Serial.*", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex LicenceSerialRegex();
    
    [GeneratedRegex(".*Yorkshire.* Region.*", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex YorkshireRegionRegex();
    
    [GeneratedRegex(".*Serial.* .*No.*", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex SerialNoRegex();
    
    [GeneratedRegex(".*Northumbria.* .*Region.*", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex NorthumbriaRegionRegex();
    
    [GeneratedRegex(".*Licence.* .*No.*", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex LicenceNoRegex();

    [GeneratedRegex(".*Anglian.*Water.*Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex AnglianWaterAuthorityRegex();
    
    [GeneratedRegex(".*NORFOLK.*AND.*SUFFOLK.*RIVER.*DIVISION", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex NofolkAndSuffolkRiverDivisionRegex();
    
    [GeneratedRegex(".*LINCOLN DIVISION", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex LincolnDivisionRegex();
    
    [GeneratedRegex(".*LINCOLNSHIRE.*RIVER.*DIVISION", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex LincolnshireRiverDivisionRegex();
    
    [GeneratedRegex(".*NORWICH.*DIVISION", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex NorwichDivisionRegex();

    [GeneratedRegex(".*Great.*Ouse.*River.*Division", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex GreatOuseRiverDivisionRegex();
    
    [GeneratedRegex(".*Colchester.*division", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex ColchesterDivisionRegex();
    
    [GeneratedRegex(".*N\\.R\\.A.*UNIT", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex NraUnitRegex();
    
    [GeneratedRegex(".*Cambridge.*Division", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex CambridgeDivisionRegex();

    [GeneratedRegex(".*WELLAND.*AND.*NENE.*RIVER.*DIVISION", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex WellandAndNeneRiverDivisionRegex();
    
    [GeneratedRegex(".*Lincoln.*Water.*Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex LincolnWaterAuthorityRegex();
    
    [GeneratedRegex(".*Lincolnshire.*River.*Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex LincolnshireRiverAuthorityRegex(); 
    
    [GeneratedRegex(".*Welland.*and.*Nene.*River.*Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex WellandAndNeneRiverAuthorityRegex();
    
    [GeneratedRegex(".*Oundle.*division", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex OundleDivisionRegex(); 
    
    [GeneratedRegex(".*East.*Suffolk.*and.*Norfolk.*River.*Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex EastSuffolkAndNorfolkRiverAuthorityRegex();
    
    [GeneratedRegex(".*Essex.*River.*Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex EssexRiverAuthorityRegex(); 
    
    [GeneratedRegex(".*Great.*Ouse.*River.*Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex GreatOuseRiverAuthorityRegex();
    
    [GeneratedRegex(".*Anglian.* Region.*", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex AnglianRegionRegex(); 
    
    [GeneratedRegex(".*Licence.* Number.*", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex LicenceNumberRegex();
    
    [GeneratedRegex(".*Mersey .*and .*Weaver .*River .*Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex MerseyAndWeaverRiverAuthorityRegex(); 
    
    [GeneratedRegex(".*Lancaster .*River .*Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex LancasterRiverAuthorityRegex();
    
    [GeneratedRegex(".*North .*West .*Water .*Authority.* -.*River .*Division", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex NorthWestWaterAuthorityRiverDivisionRegex(); 
    
    [GeneratedRegex(".*Cumberland .*River .*Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex CumberlandRiverAuthorityRegex();
    
    [GeneratedRegex(".*North.* West.* Region.*", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex NorthWestRegionRegex(); 
    
    [GeneratedRegex(".*National.* .*Rivers.* Authority", RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex NationalRiversAuthorityRegex();
}