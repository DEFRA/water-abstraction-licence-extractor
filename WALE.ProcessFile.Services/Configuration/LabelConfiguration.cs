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
            ("DateOfExpiry", GetDateOfExpiryLabels()),
            ("Issuer", GetIssuerLabels())
        ];
    }
    
    private static List<LabelToMatch> GetIssuerLabels()
    {
        return
        [
            new LabelToMatch
            {
                Name = "Issuer",
                Format = "Text",
                Text =
                [
                    new("Environment Agency"),
                    new("Lee Conservancy Catchment Board"),
                    new("National Rivers Authority"),
                    new("South Water Authority"),
                    new("Northumbrian Water Authority"),
                    new("North West Water"),
                    new("Wessex Water Authority"),
                    new("Essex River Authority"),
                    new("Thames Water Authority"),
                    new("Mersey and Weaver River Authority"),
                    new("Conservators of the The River Thames"),
                    new("Yorkshire Ouse and Hull River Authority"),
                    new("Avon and Dorset River authority"),
                    new("The Somerset River Authority"),
                    new("Southern Water Authority"),
                    new("Sussex River Authority"),
                    new("Yorkshire Water Authority")
                ],
                Possibilities = [
                    "Environment Agency",
                    "Lee Conservancy Catchment Board",
                    "National Rivers Authority",
                    "South Water Authority",
                    "Northumbrian Water Authority",
                    "North West Water",
                    "Wessex Water Authority",
                    "Essex River Authority",
                    "Thames Water Authority",
                    "Mersey and Weaver River Authority",
                    "Conservators of the The River Thames",
                    "Yorkshire Ouse and Hull River Authority",
                    "Avon and Dorset River authority",
                    "The Somerset River Authority",
                    "Southern Water Authority",
                    "Sussex River Authority",
                    "Yorkshire Water Authority"
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ApplicableToMost,
                IncludeLabelText = true
            }
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
                    new("Date of issue..."),
                    new("Date of issue ..."),
                    new("Date of Issue")
                ],
                PreviousLinesToFetch = 1,
                NextLinesToFetch = 1,
                Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
                Remove = [
                    new("...")
                ]
            },
            new LabelToMatch
            {
                Name = "DateOfIssueOldStyle",
                Format = "Text",
                Text =
                [
                    new("DATED THIS ") { LineMustStartWith = true }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ApplicableToMost
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
                    new("Date of original issue..."),
                    new("Date of original issue ..."),
                    new("Date of original issue")
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
                    new("Date effective..."),
                    new("Date effective ..."),
                    new("Date effective")
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
                    new("Date of expiry..."),
                    new("Date of expiry ...")
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
                    new("2. POINT OF ABSTRACTION") { IfMultiplePreferLast = true },
                    new("2. POINT(S) OF ABSTRACTION") { IfMultiplePreferLast = true },
                    new("2. POINTS OF ABSTRACTION") { IfMultiplePreferLast = true },
                    new("Source of supply and authorised place(s) of abstraction") { IfMultiplePreferLast = true },
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
                MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues, // Only here for 'IfMultiplePreferLast'
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
                            new("For Purpose "),
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("For Purpose ") { InstanceNumber = 2 },
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
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
                                    new("For Purpose ")
                                ],
                                Position = LabelPosition.LabelIsBeforeTextToFind,
                                Format = "Text",
                                SubLabels =
                                [
                                    new()
                                    {
                                        Name = "PurposeGroupSub",
                                        Text = [new("and ")],
                                        Position = LabelPosition.Split,
                                        MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                    }
                                ]
                            },
                            new()
                            {
                                Name = "Point",
                                TextStart = [
                                    new("2.1"),
                                    new("2.2"),
                                    new("2.3"),
                                    new("2.4"),
                                    new("2.5"),
                                    new("2.6"),
                                    new("2.7"),
                                    new("2.8"),
                                    new("2.9"),
                                    new("2.10"),
                                    new("(1)"),
                                    new("(2)"),
                                    new("(3)"),
                                    new("(4)"),
                                    new ("NZ ") { ColumnMustStartWith = true },
                                    new ("A[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("B[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("C[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("D[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("E[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("F[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("G[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("H[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("I[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("J[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("K[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("L[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("M[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("N[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("O[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("P[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Q[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("R[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("S[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("T[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("U[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("V[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("W[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("X[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Y[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new("[START_OF_BLOCK]")
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
                                    new("(2)"),
                                    new("(3)"),
                                    new("(4)"),
                                    new ("NZ ") { ColumnMustStartWith = true },
                                    new ("B[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("C[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("D[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("E[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("F[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("G[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("H[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("I[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("J[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("K[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("L[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("M[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("N[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("O[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("P[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Q[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("R[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("S[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("T[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("U[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("V[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("W[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("X[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Y[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new ("Z[END_OF_COLUMN]") { ColumnMustStartWith = true },
                                    new("[END_OF_BLOCK]")
                                ],
                                IgnoreMatchIfContains = [
                                    "At the following National Grid References as marked on the maps"
                                ],
                                Position = LabelPosition.TextToFindIsBetweenLabels,
                                Format = "Text",
                                NextLinesToFetch = 100,
                                IncludeLabelText = true,
                                MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
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
                                            "2.10",
                                            "(1)",
                                            "(2)",
                                            "(3)",
                                            "(4)"
                                        ],
                                        Position = LabelPosition.ApplicableToMost,
                                        Format = "Number"                                
                                    },
                                    new()
                                    {
                                        Name = "PurposeLink",
                                        Text = [
                                            new("For Purpose ")
                                        ],
                                        Position = LabelPosition.LabelIsBeforeTextToFind,
                                        Format = "Text",
                                        SubLabels =
                                        [
                                            new LabelToMatch
                                            {
                                                Name = "PurposeLinkSub",
                                                Text = [new("and ")],
                                                Position = LabelPosition.Split,
                                                MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues
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
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)"),                                            
                                            new("For Purpose 4.1") { RemoveWholeLine = true },
                                            new("For Purpose 4.2") { RemoveWholeLine = true },
                                            new("For Purpose 4.3") { RemoveWholeLine = true },
                                            new("For Purpose 4.4") { RemoveWholeLine = true } 
                                        ],
                                        Text = [
                                            new("marked") // TODO ' marked ' doesn't work, change so it does
                                        ],
                                        Position = LabelPosition.Split,
                                        Format = "Text",
                                        PreviousLinesToFetch = 100,
                                        NextLinesToFetch = 100,
                                        DoNotTrimLines = true
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
                    new("PURPOSE OF ABSTRACTION"),
                    new("PURPOSE(S) OF ABSTRACTION"),
                    new("PURPOSES OF ABSTRACTION"),
                    new("Purpose for which water is authorised to be used"),
                    new("Purpose(s) for which water is authorised to be used")
                ],
                TextEnd =
                [
                    new("PERIODS OF ABSTRACTION"),
                    new("PERIOD(S) OF ABSTRACTION"),
                    new("PERIOD OF ABSTRACTION"),
                    new("LAND ON WHICH LICENCE AUTHORISES USE OF WATER"),
                    new("Quantities of water authorised to be abstracted"),
                    new("QUANTITY(IES) OF WATER AUTHORISED"),
                    new("The quantity of water authorised to be abstracted shall be"),
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
                            new("From Point "),
                            new("[START_OF_BLOCK]")
                        ],
                        TextEnd = [
                            new("From Point ") { InstanceNumber = 2 },
                            new("[END_OF_BLOCK]")
                        ],
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
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
                                    new("From Point ")
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
                                        Text = [new("and ")],
                                        Position = LabelPosition.Split,
                                        MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                    }
                                ]
                            },
                            new()
                            {
                                Name = "Purpose",
                                TextStart = [
                                    new("4.1"),
                                    new("4.2"),
                                    new("4.3"),
                                    new("4.4"),
                                    new("[START_OF_BLOCK]")
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
                                MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
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
                                        MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithASingleValueButMultipleLines,
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
                    new("licence serial no:"),
                    new("licence serial no."),
                    new("serial no."),
                    new("Serial ") { LineMustStartWith = true },
                    new("ref. no. "),
                    new("Reference No."),
                    new("Reference Number "),
                    new("licence no: "),
                    new("licence no."),
                    new("Licence number: ")
                ],
                Remove = [
                    new("Licence ")
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
                    new("Licensee"),
                    new("\"hereby licence\""),
                    new("\"hereby license\""),
                    new("\"hereby licenge\""),
                    new("hereby licence ..."),
                    new("authority hereby licence"),
                    new("authority hereby license"),
                    new("authority hereby licenge"),
                    new("hereby grant a licence to"),
                    new("(hereinafter referred to as \"the Authority\")")
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                Format = "CompanyName",
                IgnoreMatchIfContains = [
                    "source of supply",
                    "abstract water"
                ]
            },
            new LabelToMatch
            {
                Text =
                [
                    new("(hereinafter referred to as \"The Licence Holder\")"),
                    new("(hereinafter referred to as \"The Licence Holder\" )"),
                    new("( hereinafter referred to as \"The Licence Holder\" )"),
                    new("( hereinafter referred to as \"The Licence Holder\")"),
                    new("(hereinafter referred to as \" The Licence Holder \")"),
                    new("(hereinafter referred to as \"The Licence Holder)"),
                    new("is hereby licensed")
                ],
                Position = LabelPosition.LabelIsAfterTextToFind,
                Format = "CompanyName",
                PreviousLinesToFetch = 7,
                IgnoreMatchIfContains = [
                    "source of supply",
                    "abstract water"
                ]
            },
            new LabelToMatch
            {
                Text =
                [
                    new("(\"the Licence Holder\")"),
                    new("(the Licence Holder\")"),
                    new("\"the Licence Holder\""),
                    new("'the Licence Holder\""),
                    new("\"the Licence Holder'")
                ],
                Position = LabelPosition.LabelIsInMiddleOfTextToFind,
                Format = "CompanyName",
                PreviousLinesToFetch = 2,
                NextLinesToFetch = 4,
                IgnoreMatchIfContains = [
                    "source of supply",
                    "abstract water"
                ]
            },
            new LabelToMatch
            {
                Text =
                [
                    new("Succession to licence"),
                    new("as amended by")
                ],
                Position = LabelPosition.ContractIsSuccession,
                Format = "CompanyName",
                MatchAllText = true,
                Name = "IsSuccession"
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
                    new("PERIOD OF ABSTRACTION"),
                    new("PERIODS OF ABSTRACTION")
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
                            new("5.1"),
                            new("5.2"),
                            new("5.3"),
                            new("5.4"),
                            new("5.5"),
                            new("5.6"),
                            new("5.7"),
                            new("5.8"),
                            new("5.9"),
                            new("5.10"),
                            new("[START_OF_BLOCK]")
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
                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
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
                                    new("For Purpose "),
                                    new("For Purposes ")
                                ],
                                Position = LabelPosition.LabelIsBeforeTextToFind,
                                Format = "Text",
                                SubLabels =
                                [
                                    new()
                                    {
                                        Name = "PurposeLinkSub",
                                        Text = [new("and ")],
                                        Position = LabelPosition.Split,
                                        MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues
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
                                MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithASingleValueButMultipleLines,
                                Position = LabelPosition.ApplicableToMost,
                                Format = "Text",
                                SubLabels = [
                                    new()
                                    {
                                        Name = "Dates",
                                        Text = [new("to ")],
                                        Remove = [
                                            new("From "),
                                            new("inclusive")
                                        ],
                                        Position = LabelPosition.Split,
                                        Format = "DateOrPurpose",
                                        MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues
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
                    new("MEANS OF ABSTRACTION")
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
                            new("3.1"),
                            new("3.2"),
                            new("3.3"),
                            new("3.4"),
                            new("[START_OF_BLOCK]")
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
                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
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
                                Text = [new("per second")],
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
                                Text = [new("per second")],
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
                                MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithASingleValueButMultipleLines,
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
                    new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED DURING THE SPECIFIED PERIOD(S)"),
                    new("MAXIMUM QUANTITY OF WATER TO BE ABSTRACTED") { IfMultiplePreferLongest = true },
                    new("MAXIMUM QUANTITIES") { ColumnMustStartWith = true },
                    new("Quantity(ies) of water authorised to be abstracted during a period"),
                    new("QUANTITY OF WATER AUTHORISED TO BE ABSTRACTED NOT EXCEEDING"),
                    new("QUANTITY OF WATER AUTHORISED TO BE ABSTRACTED DURING THE PERIOD"),
                    new("QUANTITY OF WATER AUTHORISED TO BE ABSTRACTED[END_OF_LINE]") { ColumnMustStartWith = true },
                    new("The quantity of water authorised to be abstracted shall be") { IfMultiplePreferLast = true }
                ],
                TextEnd =
                [
                    new("7. "),
                    new("MEANS OF MEASUREMENT OR ASSESSMENT OF WATER ABSTRACTED"),
                    new("MEANS OF MEASUREMENT OR ASSESSMENT OF WATER"), //" ABSTRACTED", -- Its cut off this way in a document, over 2 pages
                    new("MEANS OF MEASUREMENT OF WATER ABSTRACTED"),
                    new("Authorised means of abstraction"),
                    new("MEANS OF ABSTRACTION"),
                    new("MEANS TO BE USED FOR MEASURING"),
                    new("PERIOD(s) DURING WHICH WATER IS AUTHORIZED TO BE USED"),
                    new("Means of measurement or assessment"),
                    //new("Schedule of conditions[END_OF_LINE]") { ColumnMustStartWith = true },
                    new("8. MEANS OF ASSESSMENT OF WATER ABSTRACTED"),
                    //new("5. ") { LineMustStartWith = true },
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
                CanGoOverPageBoundary = true,
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues,
                PreviousLinesToFetch = 3,
                NextLinesToFetch = 200,
                MinimumSubMatches = 1,
                IncludeLabelText = true,
                SubLabels = new List<LabelToMatch>
                {
                    new()
                    {
                        Name = "AbstractionLimitPoint",
                        TextStart = [
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
                            new("(1)"),
                            new("(2)"),
                            new("(3)"),
                            new("(4)"),
                            new("The aggregate quantity of water authorised to be abstracted under this licence shall not") { ColumnMustStartWith = true },
                            new("[START_OF_BLOCK]")
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
                            new("(2)"),
                            new("(3)"),
                            new("(4)"),
                            new("(5)"),
                            new("The aggregate quantity of water authorised to be abstracted under this licence shall not") { ColumnMustStartWith = true },
                            new("[END_OF_BLOCK]")
                        ],
                        IncludeLabelText = true,
                        Position = LabelPosition.TextToFindIsBetweenLabels,
                        Format = "Text",
                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel,
                        PreviousLinesToFetch = 3,
                        NextLinesToFetch = 20,
                        MinimumSubMatches = 1,
                        SubLabels = new List<LabelToMatch>
                        {
                            new()
                            {
                                Name = "AbstractionLimitPointSub",
                                Text = [new("and licence")],
                                Position = LabelPosition.Split,
                                MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues,
                                PreviousLinesToFetch = 20,
                                MinimumSubMatches = 2,
                                IncludeLabelText = true,
                                SubLabels = new List<LabelToMatch>
                                {
                                    new()
                                    {
                                        Name = "DatePurpose",
                                        Text = [
                                            new("Up to and including "),
                                            new("From "),
                                            new("aggregate quantity of water authorised")
                                        ],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
                                        Format = "DateOrPurpose",
                                        IncludeLabelText = true,
                                        MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                    },
                                    new()
                                    {
                                        Name = "DatePurposeRough",
                                        Format = "Text",
                                        Text = [
                                            new("November to May"),
                                            new("June to October")
                                        ],
                                        Possibilities = [
                                            new("November to May"),
                                            new("June to October")
                                        ],
                                        PreviousLinesToFetch = 0,
                                        NextLinesToFetch = 0,
                                        Position = LabelPosition.ApplicableToMost,
                                        IncludeLabelText = true,
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel
                                    },
                                    new()
                                    {
                                        Name = "PurposeCondition",
                                        Text = [
                                            new("condition "),
                                            new("conditions ")
                                        ],
                                        TextEnd = [
                                            new("shall not exceed"),
                                            new(":")
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
                                            "4.9",
                                            "(1)",
                                            "(2)",
                                            "(3)",
                                            "(4)",
                                        ],
                                        SubLabels =
                                        [
                                            new()
                                            {
                                                Name = "PurposeConditionSub",
                                                Text = [new("and ")],
                                                Position = LabelPosition.Split,
                                                MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                            }
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "PointCondition",
                                        Text = [
                                            new("condition "),
                                            new("conditions "),
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)")
                                        ],
                                        TextEnd = [
                                            new("shall not exceed"),
                                            new(":"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)"),
                                            new("[END_OF_BLOCK]")                                            
                                        ],
                                        Position = LabelPosition.TextToFindIsBetweenLabels,
                                        IncludeLabelText = true,
                                        Format = "Text",
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
                                            "(1)",
                                            "(2)",
                                            "(3)",
                                            "(4)"
                                        ],
                                        MustContain = [
                                            "2.1",
                                            "2.2",
                                            "2.3",
                                            "2.4",
                                            "2.5",
                                            "2.6",
                                            "2.7",
                                            "2.8",
                                            "2.9",
                                            "(1)",
                                            "(2)",
                                            "(3)",
                                            "(4)"
                                        ],
                                        Remove = [
                                            new("number ")
                                        ],
                                        SubLabels =
                                        [
                                            new()
                                            {
                                                Name = "PointConditionSub",
                                                Text = [new("and ")],
                                                Position = LabelPosition.Split,
                                                MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                            }
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "LinkedLicenceNumber",
                                        Text = [
                                            new("licence number "),
                                            new("licence serial number "),
                                            new("licence serial numbers "),
                                            new("under this licence and licence"),
                                            new("and licence "),
                                            new("and under licence "),
                                            new("and under license ") // spelling mistake in licence                                    
                                        ],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
                                        Format = "LicenceNumber",
                                        MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                    },
                                    new()
                                    {
                                        Name = "LinkedLicenceFilename",
                                        Text = [
                                            new("licence number "),
                                            new("licence serial number "),
                                            new("licence serial numbers "),
                                            new("under this licence and licence"),
                                            new("and licence "),
                                            new("and under licence "),
                                            new("and under license ") // spelling mistake in licence                                    
                                        ],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
                                        Format = "LicenceNumberFilename"
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
                                        Text = [new("per hour")],
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
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                                    },
                                    new()
                                    {
                                        Name = "PerDayUnits",                                
                                        CategoryName = "PerUnits",                                
                                        Text = [new("per day")],
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
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                                    },
                                    new()
                                    {
                                        Name = "PerMonthUnits",                                
                                        CategoryName = "PerUnits",                                
                                        Text = [new("per month")],
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
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                                    },
                                    new()
                                    {
                                        Name = "PerYearUnits",                                
                                        CategoryName = "PerUnits",                                
                                        Text = [
                                            new("per year"),
                                            new("per annum")
                                        ],
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
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                                    },
                                    new()
                                    {
                                        Name = "PerSecondUnits",                                
                                        CategoryName = "PerUnits",                                
                                        Text = [new("per second")],
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
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                                    },
                                    new()
                                    {
                                        Name = "InTotalUnits",                                
                                        CategoryName = "PerUnits",                                
                                        Text = [new("in total")],
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
                                        SkipLineWhenContains = [
                                            "abstracted in total"
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "PerHourValue",                                
                                        CategoryName = "PerValue",
                                        Text = [new("per hour")],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerHourUnits",                                
                                        Format = "Number",
                                        IgnoreMatchIfContains = [
                                            "(1)",
                                            "(11)",
                                            "(111)"
                                        ],
                                        Remove = [
                                            new("6.1"),
                                            new("6.2"),
                                            new("6.3"),
                                            new("1 ")
                                            {
                                                LineMustStartWith = true,
                                                ColumnMustHave2SequentialNumbers = true
                                            },
                                            new("2 ")
                                            {
                                                LineMustStartWith = true,
                                                ColumnMustHave2SequentialNumbers = true
                                            },
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)")
                                        ],
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                                    },
                                    new()
                                    {
                                        Name = "PerDayValue",                                
                                        CategoryName = "PerValue",
                                        Text = [new("per day")],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerDayUnits",
                                        Format = "Number",
                                        IgnoreMatchIfContains = [
                                            "(1)",
                                            "(11)",
                                            "(111)"
                                        ],
                                        Remove = [
                                            new("6.1"),
                                            new("6.2"),
                                            new("6.3"),
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)")
                                        ],
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                                    },
                                    new()
                                    {
                                        Name = "PerMonthValue",                                
                                        CategoryName = "PerValue",
                                        Text = [new("per month")],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerMonthUnits",                                
                                        Format = "Number",
                                        Remove = [
                                            new("6.1"),
                                            new("6.2"),
                                            new("6.3"),
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)")
                                        ],
                                        IgnoreMatchIfContains = [
                                            "(1)",
                                            "(11)",
                                            "(111)"
                                        ],
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                                    },
                                    new()
                                    {
                                        Name = "PerYearValue",                                
                                        CategoryName = "PerValue",
                                        Text = [
                                            new("per year"),
                                            new("per annum")                                            
                                        ],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerYearUnits",
                                        Format = "Number",
                                        Remove = [
                                            new("6.1"),
                                            new("6.2"),
                                            new("6.3"),
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)")
                                        ],
                                        IgnoreMatchIfContains = [
                                            "(1)",
                                            "(11)",
                                            "(111)"
                                        ],
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                                    },
                                    new()
                                    {
                                        Name = "PerSecondValue",                                
                                        CategoryName = "PerValue",
                                        Text = [new("per second")],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "PerSecondUnits",                                
                                        Format = "Number",
                                        Remove = [
                                            new("6.1"),
                                            new("6.2"),
                                            new("6.3"),
                                            new("(1)"),
                                            new("(2)"),
                                            new("(3)"),
                                            new("(4)")
                                        ],
                                        IgnoreMatchIfContains = [
                                            "(1)",
                                            "(11)",
                                            "(111)"
                                        ],
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                                    },
                                    new()
                                    {
                                        Name = "InTotalValue",                                
                                        CategoryName = "PerValue",
                                        Text = [new("in total")],
                                        Position = LabelPosition.RelatedCategoryPosition,
                                        RelatedCategoryName = "PerUnits",
                                        RelatedName = "InTotalUnits",                                
                                        Format = "Number", // TODO add date extraction,
                                        SkipLineWhenContains = [
                                            "abstracted in total"
                                        ],
                                        IgnoreMatchIfContains = [
                                            "(1)",
                                            "(11)",
                                            "(111)"
                                        ]
                                    },
                                    new()
                                    {
                                        Name = "AYearDefinitionLine",
                                        Text = [new("beginning on")],
                                        Position = LabelPosition.LabelIsBeforeTextToFind,
                                        PreviousLinesToFetch = 0,
                                        NextLinesToFetch = 1,
                                        Format = "Text",
                                        SubLabels = [
                                            new()
                                            {
                                                Name = "AYearDates",
                                                Position = LabelPosition.Split,
                                                Text = [new("and")],
                                                Remove = [new("ending on")],
                                                Format = "DateOrPurpose",
                                                MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues
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