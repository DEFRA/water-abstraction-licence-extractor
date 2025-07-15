using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Configuration;

public static class LabelConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("Company", GetCompanyNameLabels()),
            ("LicenceNumber", GetLicenceNumberLabels()),
            ("MeansOfAbstraction", GetMeansOfAbstractionLabels()),
            ("AbstractionLimits", GetAbstractionLimitsLabels()),
            ("Purpose", GetPurposeLabels()),
            ("Points", GetPointsLabels())            
            // TODO fetch issue date, effective date and expiry date
        ];
    }
    
        private static List<LabelToMatch> GetPointsLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DocumentPoints",
                TextStart =
                [
                    "2. POINT OF ABSTRACTION",
                    "2. POINT(S) OF ABSTRACTION",
                    "2. POINTS OF ABSTRACTION"
                ],
                TextEnd =
                [
                    "MEANS OF ABSTRACTION",
                    "MEAN OF ABSTRACTION",
                    "[END_OF_BLOCK]"
                ],
                Remove =
                [
                    new(@"/Page \d* of \d*/"),
                    new("/Licence Serial No: [A-Z0-9/]*/")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MinimumSubMatches = 1,
                NextLinesToFetch = 80,
                SubLabels = new List<LabelToMatch>
                {
                    new()
                    {
                        Name = "Point",
                        TextStart = [
                            "2.1",
                            "2.2",
                            "2.3",
                            "2.4",
                            "[START_OF_BLOCK]"
                        ],
                        TextEnd = [
                            "2.2",
                            "2.3",
                            "2.4",
                            "[END_OF_BLOCK]"
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        NextLinesToFetch = 100,
                        IncludeLabelText = true,
                        Multiple = MultipleType.MultipleLabelsMultipleValues,
                        SubLabels = new List<LabelToMatch>
                        {
                            new()
                            {
                                Name  = "PointPointNumber",
                                Possibilities = [
                                    "2.1",
                                    "2.2",
                                    "2.3"
                                ],
                                Position = LabelPosition.ApplicableToAll,
                                Format = "Number"                                
                            },
                            new()
                            {
                                Name = "PurposeLink",
                                Text = [
                                    "For Purpose "
                                ],
                                Position = LabelPosition.LabelIsBeforeTextToFind,
                                Format = "ActsLikeSingleWord",
                                SubLabels =
                                [
                                    new LabelToMatch
                                    {
                                        Name = "PurposeLinkSub",
                                        Text = ["and "],
                                        Position = LabelPosition.Split
                                    }
                                ]
                            },
                            new()
                            {
                                Name = "TextWithoutPurposeAndPoint",
                                Remove = [
                                    new("2.1") { LineMustStartWith = true, RemoveWholeLine = true },
                                    new("2.2") { LineMustStartWith = true, RemoveWholeLine = true },
                                    new("2.3") { LineMustStartWith = true, RemoveWholeLine = true },
                                    new("2.4") { LineMustStartWith = true, RemoveWholeLine = true }
                                ],
                                Multiple = MultipleType.SingleLabelSingleValueMultipleLines,
                                Position = LabelPosition.ApplicableToAll,
                                Format = "Text"
                            }                            
                        }
                    }
                }
            }
        ];
    }

    private static List<LabelToMatch> GetPurposeLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DocumentPurposesAll",
                TextStart =
                [
                    "PURPOSE OF ABSTRACTION",
                    "PURPOSE(S) OF ABSTRACTION",
                    "PURPOSES OF ABSTRACTION",
                    "Purpose(s) for which water is authorised to be used"
                ],
                TextEnd =
                [
                    "PERIODS OF ABSTRACTION",
                    "PERIOD OF ABSTRACTION",
                    "[END_OF_BLOCK]"
                ],
                Remove =
                [
                    new(@"/Page \d* of \d*/"),
                    new("/Licence Serial No: [A-Z0-9/]*/")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MinimumSubMatches = 1,
                NextLinesToFetch = 30,
                SubLabels = 
                [
                    new()
                    {
                        Name = "PurposePointGroup",
                        TextStart = [
                            "From Point ",
                            "[START_OF_BLOCK]"                           
                        ],
                        TextEnd = [
                            "From Point ",
                            "[END_OF_BLOCK]"
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        Multiple = MultipleType.MultipleLabelsMultipleValues,
                        IncludeLabelText = true,
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PointGroupName",
                                Text = [
                                    "From Point "
                                ],
                                Format = "Number",
                                Position = LabelPosition.LabelIsBeforeTextToFind
                            },
                            new()
                            {
                                Name = "Purpose",
                                TextStart = [
                                    "4.1",
                                    "4.2",
                                    "4.3",
                                    "4.4",
                                    "[START_OF_BLOCK]"
                                ],
                                TextEnd = [
                                    "4.2",
                                    "4.3",
                                    "4.4",
                                    "[END_OF_BLOCK]"
                                ],
                                Position = LabelPosition.TextToFindIsBetweenLabels,
                                IncludeLabelText = true,
                                Format = "Text",
                                Multiple = MultipleType.MultipleLabelsMultipleValues,
                                Remove = [
                                    new("From Point 2.1"),
                                    new("From Point 2.2"),
                                    new(@"/Page \d* of \d*/"),
                                    new("/Licence Serial No: [A-Z0-9/]*/")
                                    /* TODO add flag to include parent removes */
                                ],
                                SubLabels =
                                [
                                    new()
                                    {
                                        Name = "PointLink",
                                        Text = [
                                            "From Point "
                                        ],
                                        Position = LabelPosition.LabelIsBeforeTextToFind,
                                        Format = "SingleWord"
                                    },
                                    new()
                                    {
                                        Name = "PointNumber",
                                        Possibilities = [
                                            "4.1",
                                            "4.2",
                                            "4.3"
                                        ],
                                        Position = LabelPosition.ApplicableToAll,
                                        Format = "Number"
                                    },
                                    new()
                                    {
                                        Name = "TextWithoutPoints",
                                        Remove = [
                                            new("From Point 2.1"),
                                            new("From Point 2.2"),
                                            new("From Point 2.3"),
                                            new("From Point 2.4"),
                                            new("From Point 2.5"),
                                            new("From Point 2.6"),
                                            new("From Point 2.7"),
                                            new("From Point 2.8"),
                                            new("From Point 2.9"),
                                            new("4.1"),
                                            new("4.2"),
                                            new("4.3"),
                                            new("4.4")
                                        ],
                                        Multiple = MultipleType.SingleLabelSingleValueMultipleLines,
                                        Position = LabelPosition.ApplicableToAll,
                                        Format = "Text"
                                    }                            
                                ]
                            }
                        ]
                    }
                ]
            }
        ];
    }
    
    private static List<LabelToMatch> GetLicenceNumberLabels()
    {
        return
        [
            new LabelToMatch
            {
                Text =
                [
                    "licence serial no:",
                    "licence serial no.",
                    "serial no.",
                    "ref. no. ",
                    "Reference No.",                    
                    "licence no: ",
                    "licence no.",
                    "Licence number: "
                ],
                Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
                Format = "LicenceNumber"
            }
        ];
    }

    private static List<LabelToMatch> GetCompanyNameLabels()
    {
        return
        [
            new LabelToMatch
            {
                Text =
                [
                    "Licensee",
                    "\"hereby licence\"",
                    "\"hereby license\"",
                    "\"hereby licenge\"",
                    "hereby licence ...",
                    "authority hereby licence",
                    "authority hereby license",
                    "authority hereby licenge",
                    "hereby grant a licence to"
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                Format = "CompanyName"
            },
            new LabelToMatch
            {
                Text =
                [
                    "(hereinafter referred to as \"The Licence Holder\")",
                    "( hereinafter referred to as \"The Licence Holder\" )",
                    "(hereinafter referred to as \" The Licence Holder \")",
                    "(hereinafter referred to as \"The Licence Holder)",
                    "is hereby licensed"
                ],
                Position = LabelPosition.LabelIsAfterTextToFind,
                Format = "CompanyName"
            },
            new LabelToMatch
            {
                Text =
                [
                    "(\"the Licence Holder\")",
                    "(the Licence Holder\")",
                    "\"the Licence Holder\""
                ],
                Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                Format = "CompanyName",
                PreviousLinesToFetch = 3,
                NextLinesToFetch = 10
            },
            new LabelToMatch
            {
                Text =
                [
                    "Succession to licence",
                    "as amended by"
                ],
                Position = LabelPosition.ContractIsSuccession,
                Format = "CompanyName",
                MatchAllText = true
            }
        ];
    }

    private static List<LabelToMatch> GetMeansOfAbstractionLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DocumentMeansOfAbstractionSection",
                TextStart =
                [
                    "MEANS OF ABSTRACTION"
                ],
                TextEnd =
                [
                    "PURPOSE OF ABSTRACTION",
                    "[END_OF_BLOCK]"
                ],
                Remove =
                [
                    new("3.1")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                PreviousLinesToFetch = 3,
                NextLinesToFetch = 20,
                SubLabels = new List<LabelToMatch>
                {
                    new()
                    {
                        Name = "PerSecondUnitsMeans",                                
                        CategoryName = "PerUnits",                                
                        Text = ["per second"],
                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                        Format = "Units",
                        Possibilities = new List<string>
                        {
                            "megalitres",
                            "litres",
                            "cubic metres",
                            "megagallons",
                            "thousand gallons",
                            "million gallons",
                            "gallons"                                    
                        }
                    },
                    new()
                    {
                        Name = "PerSecondValueMeans",                                
                        CategoryName = "PerValue",
                        Text = ["per second"],
                        Position = LabelPosition.RelatedCategoryPosition,
                        RelatedCategoryName = "PerUnits",
                        RelatedName = "PerSecondUnits",                                
                        Format = "Number"
                    }
                }
            }
        ];
    }

    private static List<LabelToMatch> GetAbstractionLimitsLabels()
    {
        // TODO pull the purposes and can verify them against the NALD data
        
        return
        [
            new LabelToMatch
            {
                Name = "DocumentAbstractionLimitsSection",
                TextStart =
                [
                    "MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE SPECIFIED PERIOD(S)",
                    "MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED",
                    "MAXIMUM QUANTITIES",
                    "Quantity(ies) of water authorised to be abstracted during a period",
                    "QUANTITY OF WATER AUTHORISED TO BE ABSTRACTED"
                ],
                TextEnd =
                [
                    "7. ",
                    "MEANS OF MEASUREMENT OR ASSESSMENT OF WATER ABSTRACTED",
                    "MEANS OF MEASUREMENT OR ASSESSMENT OF WATER", //" ABSTRACTED", -- Its cut off this way in a document, over 2 pages
                    "MEANS OF MEASUREMENT OF WATER ABSTRACTED",
                    "MEANS OF ABSTRACTION",
                    "Authorised means of abstraction",
                    "MEANS TO BE USED FOR MEASURING",
                    "PERIOD(s) DURING WHICH WATER IS AUTHORIZED TO BE USED",
                    "[END_OF_BLOCK]"
                ],
                MustContain =
                [
                    "cubic metres",
                    "cubic meters", // Some files have this US spelling
                    " m per", // This is wrong but its how it gets read in some files
                    "m\u00b3", // m3
                    "gallons",
                    "litres"
                ],
                Remove =
                [
                    new("6.1"),
                    new("6.2"),
                    new("6.3"),
                    new("6.4"),
                    new("6.5"),
                    new("6.6"),
                    new("6.7"),
                    new("6.8"),
                    new("6.9"),
                    new("6.10"),
                    new(@"/Page \d* of \d*/"),
                    new("/Licence Serial No: [A-Z0-9/]*/")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                PreviousLinesToFetch = 3,
                NextLinesToFetch = 200,
                MinimumSubMatches = 1,
                SubLabels = new List<LabelToMatch>
                {
                    new()
                    {
                        Name = "AbstractionLimitPoint",
                        TextStart = [
                            "6.1",
                            "6.2",
                            "6.3",
                            "6.4",
                            "6.5",
                            "6.6",
                            "6.7",
                            "6.8",
                            "6.9",
                            "6.10",
                            "[START_OF_BLOCK]"
                        ],
                        TextEnd = [
                            "6.2",
                            "6.3",
                            "6.4",
                            "6.5",
                            "6.6",
                            "6.7",
                            "6.8",
                            "6.9",
                            "6.10",                            
                            "[END_OF_BLOCK]"
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        Multiple = MultipleType.MultipleLabelsMultipleValues,
                        PreviousLinesToFetch = 3,
                        NextLinesToFetch = 20,
                        MinimumSubMatches = 1,
                        SubLabels = new List<LabelToMatch>
                        {
                            new()
                            {
                                Name = "AbstractionLimitPointSub",
                                Text = ["and licence"],
                                Position = LabelPosition.Split,
                                PreviousLinesToFetch = 20,
                                MinimumSubMatches = 2,
                                SubLabels = new List<LabelToMatch>
                                {
                                    new()
                                    {
                                        Name = "PointPurpose",
                                        Text = [
                                            "Up to and including ",
                                            "From ",
                                            "aggregate quantity of water authorised"
                                        ],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
                                        Format = "DateOrPurpose",
                                        IncludeLabelText = true
                                    },
                                    new()
                                    {
                                        Name = "LinkedLicenceNumber",
                                        Text = [
                                            "licence number ",
                                            "licence serial number ",
                                            "licence serial numbers ",
                                            "under this licence and licence",
                                            "and licence ",
                                            "and under licence ",
                                            "and under license " // spelling mistake in licence                                    
                                        ],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
                                        Format = "LicenceNumber",
                                        Multiple = MultipleType.SingleLabelMultipleValues
                                    },
                                    new()
                                    {
                                        Name = "LinkedLicence",
                                        RelatedName = "LinkedLicenceNumber",
                                        Format = "LinkedLicence",
                                    },
                                    new()
                                    {
                                        Name = "PerHourUnits",
                                        CategoryName = "PerUnits",
                                        Text = ["per hour"],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                                        Format = "Units",
                                        Possibilities = new List<string>
                                        {
                                            "megalitres",
                                            "litres",
                                            "cubic metres",
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        }
                                    },
                                    new()
                                    {
                                        Name = "PerDayUnits",                                
                                        CategoryName = "PerUnits",                                
                                        Text = ["per day"],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                                        Format = "Units",
                                        Possibilities = new List<string>
                                        {
                                            "megalitres",
                                            "litres",
                                            "cubic metres",
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        }
                                    },
                                    new()
                                    {
                                        Name = "PerMonthUnits",                                
                                        CategoryName = "PerUnits",                                
                                        Text = ["per month"],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                                        Format = "Units",
                                        Possibilities = new List<string>
                                        {
                                            "megalitres",
                                            "litres",
                                            "cubic metres",
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        }
                                    },
                                    new()
                                    {
                                        Name = "PerYearUnits",                                
                                        CategoryName = "PerUnits",                                
                                        Text = ["per year"],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                                        Format = "Units",
                                        Possibilities = new List<string>
                                        {
                                            "megalitres",
                                            "litres",
                                            "cubic metres",
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        }
                                    },
                                    new()
                                    {
                                        Name = "PerSecondUnits",                                
                                        CategoryName = "PerUnits",                                
                                        Text = ["per second"],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                                        Format = "Units",
                                        Possibilities = new List<string>
                                        {
                                            "megalitres",
                                            "litres",
                                            "cubic metres",
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        }
                                    },
                                    new()
                                    {
                                        Name = "InTotalUnits",                                
                                        CategoryName = "PerUnits",                                
                                        Text = ["in total"],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                                        Format = "Units",
                                        Possibilities = new List<string>
                                        {
                                            "megalitres",
                                            "litres",
                                            "cubic metres",
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        }
                                    },
                                    new()
                                    {
                                        Name = "PerHourValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["per hour"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerHourUnits",                                
                                        Format = "Number"
                                    },
                                    new()
                                    {
                                        Name = "PerDayValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["per day"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerDayUnits",
                                        Format = "Number"
                                    },
                                    new()
                                    {
                                        Name = "PerMonthValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["per month"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerMonthUnits",                                
                                        Format = "Number"
                                    },
                                    new()
                                    {
                                        Name = "PerYearValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["per year"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerYearUnits",                                
                                        Format = "Number"
                                    },
                                    new()
                                    {
                                        Name = "PerSecondValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["per second"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerSecondUnits",                                
                                        Format = "Number"
                                    },
                                    new()
                                    {
                                        Name = "InTotalValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["in total"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "InTotalUnits",                                
                                        Format = "Number" // TODO add date extraction
                                    }
                                }       
                            }
                        }
                    }
                }
            }
        ];
    }
}