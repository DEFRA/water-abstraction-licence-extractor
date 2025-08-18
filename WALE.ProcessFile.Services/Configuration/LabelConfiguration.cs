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
            ("PeriodsOfAbstraction", GetPeriodsOfAbstractionLabels()),
            ("AbstractionLimits", GetAbstractionLimitsLabels()),
            ("Purpose", GetPurposeLabels()),
            ("Points", GetPointsLabels()),
            ("DateOfIssue", GetDateOfIssueLabels()),
            ("DateOfOriginalIssue", GetDateOfOriginalIssueLabels()),
            ("DateEffective", GetDateEffectiveLabels()),
            ("DateOfExpiry", GetDateOfExpiryLabels())
        ];
    }

    private static List<LabelToMatch> GetDateOfIssueLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DateOfIssue",
                Format = "DateOrPurpose",
                Text =
                [
                    "Date of issue...",
                    "Date of issue ...",
                    "Date of Issue"
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                Remove = [
                    new("...")
                ],
                NextLinesToFetch = 1
            }
        ];
    }
    
    private static List<LabelToMatch> GetDateOfOriginalIssueLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DateOfOriginalIssue",
                Format = "DateOrPurpose",
                Text =
                [
                    "Date of original issue...",
                    "Date of original issue ...",
                    "Date of original issue"
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                Remove = [
                    new("...")
                ],
                NextLinesToFetch = 1
            }
        ];
    }
    
    private static List<LabelToMatch> GetDateEffectiveLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DateEffective",
                Format = "DateOrPurpose",
                Text =
                [
                    "Date effective...",
                    "Date effective ...",
                    "Date effective"
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                Remove = [
                    new("...")
                ],
                NextLinesToFetch = 1
            }
        ];
    }    
    
    private static List<LabelToMatch> GetDateOfExpiryLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DateOfExpiry",
                Format = "DateOrPurpose",
                Text =
                [
                    "Date of expiry...",
                    "Date of expiry ..."
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                Remove = [
                    new("...")
                ],
                NextLinesToFetch = 1
            }
        ];
    }
    
    private static List<LabelToMatch> GetPointsLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DocumentPointsAll",
                TextStart =
                [
                    "2. POINT OF ABSTRACTION",
                    "2. POINT(S) OF ABSTRACTION",
                    "2. POINTS OF ABSTRACTION"
                ],
                TextEnd =
                [
                    new("MEANS OF ABSTRACTION"),
                    new("MEAN OF ABSTRACTION"),
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    new(@"/Page \d* of \d*/"),
                    new("/Licence Serial No: [A-Z0-9/]*/")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                IncludeWholeLine = true,
                MinimumSubMatches = 1,
                NextLinesToFetch = 100,
                SubLabels = new List<LabelToMatch>
                {
                    new()
                    {
                        Name = "PointPurposeGroup",
                        TextStart = [
                            "For Purpose ",
                            "[START_OF_BLOCK]"                           
                        ],
                        TextEnd = [
                            new("For Purpose ") { InstanceNumber = 2 },
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        Multiple = MultipleType.MultipleLabelsMultipleValues,
                        IncludeWholeLine = true,
                        NextLinesToFetch = 100,
                        Remove = [
                            new("2. POINT OF ABSTRACTION"),
                            new("2. POINT(S) OF ABSTRACTION"),
                            new("2. POINTS OF ABSTRACTION")
                        ],
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PurposeGroupName",
                                Text = [
                                    "For Purpose "
                                ],
                                Position = LabelPosition.LabelIsBeforeTextToFind,
                                Format = "Text",
                                SubLabels =
                                [
                                    new()
                                    {
                                        Name = "PurposeGroupSub",
                                        Text = ["and "],
                                        Position = LabelPosition.Split
                                    }
                                ]
                            },
                            new()
                            {
                                Name = "Point",
                                TextStart = [
                                    "2.1",
                                    "2.2",
                                    "2.3",
                                    "2.4",
                                    "2.5",
                                    "2.6",
                                    "2.7",
                                    "2.8",
                                    "2.9",
                                    "2.10",
                                    "[START_OF_BLOCK]"
                                ],
                                TextEnd = [
                                    new("2.2"),
                                    new("2.3"),
                                    new("2.4"),
                                    new("2.5"),
                                    new("2.6"),
                                    new("2.7"),
                                    new("2.8"),
                                    new("2.9"),
                                    new("2.10"),
                                    new("2.11"),
                                    new("[END_OF_BLOCK]")
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
                                            "2.3",
                                            "2.4",
                                            "2.5",
                                            "2.6",
                                            "2.7",
                                            "2.8",
                                            "2.9",
                                            "2.10"
                                        ],
                                        Position = LabelPosition.ApplicableToMost,
                                        Format = "Number"                                
                                    },
                                    new()
                                    {
                                        Name = "PurposeLink",
                                        Text = [
                                            "For Purpose "
                                        ],
                                        Position = LabelPosition.LabelIsBeforeTextToFind,
                                        Format = "Text",
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
                                            new("2.1") { ColumnMustStartWith = true },
                                            new("2.2") { ColumnMustStartWith = true },
                                            new("2.3") { ColumnMustStartWith = true },
                                            new("2.4") { ColumnMustStartWith = true },
                                            new("2.5") { ColumnMustStartWith = true },
                                            new("2.6") { ColumnMustStartWith = true },
                                            new("2.7") { ColumnMustStartWith = true },
                                            new("2.8") { ColumnMustStartWith = true },
                                            new("2.9") { ColumnMustStartWith = true },
                                            new("2.10") { ColumnMustStartWith = true },                                          
                                            new("For Purpose 4.1") { RemoveWholeLine = true },
                                            new("For Purpose 4.2") { RemoveWholeLine = true },
                                            new("For Purpose 4.3") { RemoveWholeLine = true },
                                            new("For Purpose 4.4") { RemoveWholeLine = true }                                    
                                        ],
                                        Multiple = MultipleType.SingleLabelSingleValueMultipleLines,
                                        Position = LabelPosition.ApplicableToMost,
                                        Format = "Text",
                                        NextLinesToFetch = 100,
                                    }
                                }
                            }
                        ]
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
                    new("PERIODS OF ABSTRACTION"),
                    new("PERIOD(S) OF ABSTRACTION"),
                    new("PERIOD OF ABSTRACTION"),
                    new("LAND ON WHICH LICENCE AUTHORISES USE OF WATER"),
                    new("Quantities of water authorised to be abstracted"),
                    new("QUANTITY(IES) OF WATER AUTHORISED"),
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    new(@"/Page \d* of \d*/"),
                    new("/Licence Serial No: [A-Z0-9/]*/")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                IncludeWholeLine = true,
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
                            new("From Point ") { InstanceNumber = 2 },
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        Multiple = MultipleType.MultipleLabelsMultipleValues,
                        IncludeWholeLine = true,
                        Remove = [
                            new("4. PURPOSE OF ABSTRACTION"),
                            new("4. PURPOSE(S) OF ABSTRACTION"),
                            new("4. PURPOSES OF ABSTRACTION"),
                            new("PURPOSE OF ABSTRACTION"),
                            new("PURPOSES OF ABSTRACTION"),
                            new("PURPOSE(S) OF ABSTRACTION"),
                            new("Purpose(s) for which water is authorised to be used"),
                            new("PURPOSE(S) FOR WHICH WATER IS AUTHORISED TO BE USED") // TODO why does the capitalisation matter here?
                        ],
                        SubLabels =
                        [
                            new()
                            {
                                Name = "PointGroupName",
                                Text = [
                                    "From Point "
                                ],
                                Format = "Number",
                                Position = LabelPosition.LabelIsBeforeTextToFind,
                                Possibilities = [
                                    "2.1",
                                    "2.2",
                                    "2.3",
                                    "2.4",
                                    "2.5",
                                    "2.6",
                                    "2.7",
                                    "2.8",
                                    "2.9",
                                    "2.10"                                    
                                ],
                                SubLabels =
                                [
                                    new()
                                    {
                                        Name = "PointGroupSub",
                                        Text = ["and "],
                                        Position = LabelPosition.Split
                                    }
                                ]
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
                                    new("4.2"),
                                    new("4.3"),
                                    new("4.4"),
                                    new("[END_OF_BLOCK]")
                                ],
                                Position = LabelPosition.TextToFindIsBetweenLabels,
                                IncludeLabelText = true,
                                Format = "Text",
                                Multiple = MultipleType.MultipleLabelsMultipleValues,
                                //Remove = [
                                //    new(@"/Page \d* of \d*/"),
                                //    new("/Licence Serial No: [A-Z0-9/]*/")
                                //    /* TODO add flag to include parent removes */
                                //],
                                SubLabels =
                                [
                                    new()
                                    {
                                        Name  = "PurposeNumber",
                                        Possibilities = [
                                            "4.1",
                                            "4.2",
                                            "4.3"
                                        ],
                                        Position = LabelPosition.ApplicableToMost,
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
                                            new("From Point 2.10"),
                                            new("4.1"),
                                            new("4.2"),
                                            new("4.3"),
                                            new("4.4")
                                        ],
                                        Multiple = MultipleType.SingleLabelSingleValueMultipleLines,
                                        Position = LabelPosition.ApplicableToMost,
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
                    "Reference Number ",
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
                    "hereby grant a licence to",
                    "(hereinafter referred to as \"the Authority\")",
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                Format = "CompanyName",
                ResultMustNotContain = [
                    "source of supply",
                    "abstract water"
                ]
            },
            new LabelToMatch
            {
                Text =
                [
                    "(hereinafter referred to as \"The Licence Holder\")",
                    "(hereinafter referred to as \"The Licence Holder\" )",
                    "( hereinafter referred to as \"The Licence Holder\" )",
                    "( hereinafter referred to as \"The Licence Holder\")",
                    "(hereinafter referred to as \" The Licence Holder \")",
                    "(hereinafter referred to as \"The Licence Holder)",
                    "is hereby licensed"
                ],
                Position = LabelPosition.LabelIsAfterTextToFind,
                Format = "CompanyName",
                PreviousLinesToFetch = 7,
                ResultMustNotContain = [
                    "source of supply",
                    "abstract water"
                ]
            },
            new LabelToMatch
            {
                Text =
                [
                    "(\"the Licence Holder\")",
                    "(the Licence Holder\")",
                    "\"the Licence Holder\"",
                    "'the Licence Holder\"",
                    "\"the Licence Holder'"
                ],
                Position = LabelPosition.LabelIsInMiddleOfTextToFind,
                Format = "CompanyName",
                PreviousLinesToFetch = 2,
                NextLinesToFetch = 4,
                ResultMustNotContain = [
                    "source of supply",
                    "abstract water"
                ]
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

    private static List<LabelToMatch> GetPeriodsOfAbstractionLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "DocumentPeriodsOfAbstractionSection",
                TextStart =
                [
                    "PERIOD OF ABSTRACTION",
                    "PERIODS OF ABSTRACTION"
                ],
                TextEnd =
                [
                    new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED"),
                    new("FURTHER CONDITIONS"),
                    new("[END_OF_BLOCK]")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 15,
                SubLabels = [
                    new()
                    {
                        Name = "PeriodOfAbstractionSubSection",
                        TextStart = [
                            "5.1",
                            "5.2",
                            "5.3",
                            "5.4",
                            "5.5",
                            "5.6",
                            "5.7",
                            "5.8",
                            "5.9",
                            "5.10",
                            "[START_OF_BLOCK]"
                        ],
                        TextEnd = [
                            new("5.2"),
                            new("5.3"),
                            new("5.4"),
                            new("5.5"),
                            new("5.6"),
                            new("5.7"),
                            new("5.8"),
                            new("5.9"),
                            new("5.10"),
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        Multiple = MultipleType.MultipleLabelsMultipleValues,
                        PreviousLinesToFetch = 0,
                        NextLinesToFetch = 10,
                        IncludeLabelText = true,
                        SubLabels =
                        [
                            new()
                            {
                                Name  = "PeriodPeriodNumber",
                                Possibilities = [
                                    "5.1",
                                    "5.2",
                                    "5.3"
                                ],
                                Position = LabelPosition.ApplicableToMost,
                                Format = "Number"                                
                            },
                            new()
                            {
                                Name = "PurposeLink",
                                Text = [
                                    "For Purpose ",
                                    "For Purposes "
                                ],
                                Position = LabelPosition.LabelIsBeforeTextToFind,
                                Format = "Text",
                                SubLabels =
                                [
                                    new()
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
                                    new("5.1") { ColumnMustStartWith = true },
                                    new("5.2") { ColumnMustStartWith = true },
                                    new("5.3") { ColumnMustStartWith = true },
                                    new("5.4") { ColumnMustStartWith = true },
                                    new("For Purpose ") { RemoveWholeLine = true },
                                    new("For Purposes ") { RemoveWholeLine = true }                                   
                                ],
                                Multiple = MultipleType.SingleLabelSingleValueMultipleLines,
                                Position = LabelPosition.ApplicableToMost,
                                Format = "Text",
                                SubLabels = [
                                    new()
                                    {
                                        Name = "Dates",
                                        Text = ["to "],
                                        Remove = [
                                            new("From "),
                                            new("inclusive")
                                        ],
                                        Position = LabelPosition.Split,
                                        Format = "DateOrPurpose"
                                    }
                                ]
                            }
                        ]
                    }
                ]
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
                    new("PURPOSE OF ABSTRACTION"),
                    new("[END_OF_BLOCK]")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                PreviousLinesToFetch = 3,
                NextLinesToFetch = 20,
                SubLabels =
                [
                    new()
                    {
                        Name = "Mean",
                        TextStart = [
                            "3.1",
                            "3.2",
                            "3.3",
                            "3.4",
                            "[START_OF_BLOCK]"
                        ],
                        TextEnd = [
                            new("3.2"),
                            new("3.3"),
                            new("3.4"),
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        NextLinesToFetch = 6,
                        IncludeLabelText = true,
                        Multiple = MultipleType.MultipleLabelsMultipleValues,
                        SubLabels =
                        [
                            new()
                            {
                                Name  = "MeanId",
                                Possibilities = [
                                    "3.1",
                                    "3.2",
                                    "3.3"
                                ],
                                Position = LabelPosition.ApplicableToMost,
                                Format = "Number"                                
                            },
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
                                Format = "Number",
                                Remove =
                                [
                                    new("3.1"),
                                    new("3.2"),
                                    new("3.3"),
                                    new("3.4")
                                ]
                            },
                            new()
                            {
                                Name = "TextWithoutNumber",
                                Remove = [
                                    new("3.1") { ColumnMustStartWith = true },
                                    new("3.2") { ColumnMustStartWith = true },
                                    new("3.3") { ColumnMustStartWith = true },
                                    new("3.4") { ColumnMustStartWith = true }
                                ],
                                Multiple = MultipleType.SingleLabelSingleValueMultipleLines,
                                Position = LabelPosition.ApplicableToMost,
                                Format = "Text"
                            }
                        ]
                    }
                ]
            }
        ];
    }

    private static List<LabelToMatch> GetAbstractionLimitsLabels()
    {
        // TODO Verify the purposes against the NALD data
        
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
                    "QUANTITY OF WATER AUTHORISED TO BE ABSTRACTED NOT EXCEEDING",
                    "QUANTITY OF WATER AUTHORISED TO BE ABSTRACTED DURING THE PERIOD",
                    "QUANTITY OF WATER AUTHORISED TO BE ABSTRACTED[END_OF_LINE]"
                ],
                TextEnd =
                [
                    new("7. "),
                    new("MEANS OF MEASUREMENT OR ASSESSMENT OF WATER ABSTRACTED"),
                    new("MEANS OF MEASUREMENT OR ASSESSMENT OF WATER"), //" ABSTRACTED", -- Its cut off this way in a document, over 2 pages
                    new("MEANS OF MEASUREMENT OF WATER ABSTRACTED"),
                    new("MEANS OF ABSTRACTION"),
                    new("Authorised means of abstraction"),
                    new("MEANS TO BE USED FOR MEASURING"),
                    new("PERIOD(s) DURING WHICH WATER IS AUTHORIZED TO BE USED"),
                    new("[END_OF_BLOCK]")
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
                            new("6.2"),
                            new("6.3"),
                            new("6.4"),
                            new("6.5"),
                            new("6.6"),
                            new("6.7"),
                            new("6.8"),
                            new("6.9"),
                            new("6.10"),
                            new("[END_OF_BLOCK]")
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
                                        Name = "DatePurpose",
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
                                        Name = "PurposeCondition",
                                        Text = [
                                            "condition ",
                                            "conditions "
                                        ],
                                        TextEnd = [
                                            new("shall not exceed")
                                        ],
                                        Position = LabelPosition.TextToFindIsBetweenLabels,
                                        Format = "Text",
                                        MustContain = [
                                            "4.1",
                                            "4.2",
                                            "4.3",
                                            "4.4",
                                            "4.5",
                                            "4.6",
                                            "4.7",
                                            "4.8",
                                            "4.9"
                                        ],
                                        SubLabels =
                                        [
                                            new()
                                            {
                                                Name = "PurposeConditionSub",
                                                Text = ["and "],
                                                Position = LabelPosition.Split
                                            }
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "PointCondition",
                                        Text = [
                                            "condition ",
                                            "conditions "
                                        ],
                                        TextEnd = [
                                            new("shall not exceed")
                                        ],
                                        Position = LabelPosition.TextToFindIsBetweenLabels,
                                        Format = "Text",
                                        MustContain = [
                                            "2.1",
                                            "2.2",
                                            "2.3",
                                            "2.4",
                                            "2.5",
                                            "2.6",
                                            "2.7",
                                            "2.8",
                                            "2.9"
                                        ],
                                        SubLabels =
                                        [
                                            new()
                                            {
                                                Name = "PurposeConditionSub",
                                                Text = ["and "],
                                                Position = LabelPosition.Split
                                            }
                                        ]
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
                                        Name = "LinkedLicenceFilename",
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
                                        Format = "LicenceNumberFilename",
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
                                        },
                                        LabelLineMustNotContain = [
                                            "abstracted in total"
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "PerHourValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["per hour"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerHourUnits",                                
                                        Format = "Number",
                                        Remove = [
                                            new("6.1"),
                                            new("6.2"),
                                            new("6.3"),
                                            new("1 ")
                                            {
                                                ColumnMustStartWith  = true,
                                                ColumnMustHave2SequentialNumbers = true
                                            },
                                            new("2 ")
                                            {
                                                ColumnMustStartWith  = true,
                                                ColumnMustHave2SequentialNumbers = true
                                            },
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "PerDayValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["per day"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerDayUnits",
                                        Format = "Number",
                                        Remove = [
                                            new("6.1"),
                                            new("6.2"),
                                            new("6.3")
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "PerMonthValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["per month"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerMonthUnits",                                
                                        Format = "Number",
                                        Remove = [
                                            new("6.1"),
                                            new("6.2"),
                                            new("6.3")
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "PerYearValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["per year"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerYearUnits",                                
                                        Format = "Number",
                                        Remove = [
                                            new("6.1"),
                                            new("6.2"),
                                            new("6.3")
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "PerSecondValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["per second"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerSecondUnits",                                
                                        Format = "Number",
                                        Remove = [
                                            new("6.1"),
                                            new("6.2"),
                                            new("6.3")
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "InTotalValue",                                
                                        CategoryName = "PerValue",
                                        Text = ["in total"],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "InTotalUnits",                                
                                        Format = "Number", // TODO add date extraction,
                                        LabelLineMustNotContain = [
                                            "abstracted in total"
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "AYearDefinitionLine",
                                        Text = ["beginning on"],
                                        Position = LabelPosition.LabelIsBeforeTextToFind,
                                        PreviousLinesToFetch = 0,
                                        NextLinesToFetch = 1,
                                        Format = "Text",
                                        SubLabels = [
                                            new()
                                            {
                                                Name = "AYearDates",
                                                Position = LabelPosition.Split,
                                                Text = ["and"],
                                                Remove = [new("ending on")],
                                                Format = "DateOrPurpose"
                                            }
                                        ]
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