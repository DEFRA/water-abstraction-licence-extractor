using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Configuration;

public static class LicenceReaderConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("Company", GetCompanyNameLabels()),
            ("LicenceNumber", GetLicenceNumberLabels()),
            ("DateOfIssue", GetDateOfIssueLabels())
        ];
    }
    
    private static List<LabelToMatch> GetRecords()
    {
        return
        [
            new LabelToMatch
            {
                Name = "RecordsAll",
                TextStart =
                [
                    new("8. Records[END_OF_LINE]"),// Addendum
                    new("Records[END_OF_LINE]") { LineMustStartWith = true }
                ],
                TextEnd =
                [
                    new("9. Further conditions"), // 
                    new("Further Conditions[END_OF_LINE]") { LineMustStartWith = true },
                    new("Additional Information[END_OF_LINE]") { LineMustStartWith = true },
                    new("Would you like to find out") { LineMustStartWith = true },
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    new(@"/Page \d* of \d*/"),
                    new("/Licence Serial No: [A-Z0-9\\/\\. ]{3,16}/")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                IncludeWholeLine = true,
                NextLinesToFetch = 100,
                SubLabels = 
                [
                    new()
                    {
                        Name = "RecordsLinkedLicenceNumber",
                        Text =
                        [
                            new(LicenceNumber.RegexPatten)
                            {
                                IsRegularExpression = true
                            }
                        ],
                        Format = LicenceNumber.Constant,
                        Position = LabelPosition.ActuallyLabel,
                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        SkipLineWhenContains =
                        [
                            new("Licence Serial No: ")
                        ]
                    }
                ]
            }
        ];
    }
    
    private static List<LabelToMatch> GetAdditional()
    {
        return
        [
            new LabelToMatch
            {
                Name = "AdditionalAll",
                TextStart =
                [
                    new("ADDITIONAL INFORMATION[END_OF_LINE]") { LineMustStartWith = true },
                    new("ADDITIONAL[END_OF_LINE]") { LineMustStartWith = true }
                ],
                TextEnd =
                [
                    new("History of licence[END_OF_LINE]") { LineMustStartWith = true },
                    new("Licence History[END_OF_LINE]") { LineMustStartWith = true },
                    new("Would you like to find out") { LineMustStartWith = true },
                    new("Map accompanying licence number"),
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    new(@"/Page \d* of \d*/"),
                    new("/Licence Serial No: [A-Z0-9\\/\\. ]{3,16}/")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                IncludeWholeLine = true,
                NextLinesToFetch = 100,
                SubLabels = 
                [
                    new()
                    {
                        Name = "AdditionalLinkedLicenceNumber",
                        Text =
                        [
                            new(LicenceNumber.RegexPatten)
                            {
                                IsRegularExpression = true
                            }
                        ],
                        Format = LicenceNumber.Constant,
                        Position = LabelPosition.ActuallyLabel,
                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        SkipLineWhenContains =
                        [
                            new("Licence Serial No: ")
                        ]
                    }
                ]
            }
        ];
    }

    private static List<LabelToMatch> GetFurtherConditions()
    {
        return
        [
            new LabelToMatch
            {
                Name = "FurtherConditionsAll",
                TextStart =
                [
                    new("FURTHER CONDITIONS[END_OF_LINE]")
                ],
                TextEnd =
                [
                    new("ADDITIONAL INFORMATION[END_OF_LINE]") { LineMustStartWith = true },
                    new("[END_OF_BLOCK]")
                ],
                Remove =
                [
                    new(@"/Page \d* of \d*/"),
                    new("/Licence Serial No: [A-Z0-9\\/\\. ]{3,16}/")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                IncludeWholeLine = true,
                NextLinesToFetch = 60,
                SubLabels = 
                [
                    new()
                    {
                        Name = "FCLinkedLicenceNumber",
                        Text =
                        [
                            new(LicenceNumber.RegexPatten)
                            {
                                IsRegularExpression = true
                            }
                        ],
                        Format = LicenceNumber.Constant,
                        Position = LabelPosition.ActuallyLabel,
                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                        SkipLineWhenContains =
                        [
                            new("Licence Serial No: ")
                        ]
                    }
                ]
            }
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
                IncludeStartLabelText = true
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
                    new("DATED THIS") { LineMustStartWith = true }
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ApplicableToMost
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
                Format = LicenceNumber.Constant,
                Name = "DocumentLicenceNumber"
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
                    new("CONDITIONS[END_OF_LINE]") { LineMustStartWith = true},
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
                    new("/Licence Serial No: [A-Z0-9\\/\\. ]{3,16}/")
                ],
                CanGoOverPageBoundary = true,
                Position = LabelPosition.TextToFindIsBetweenLabels,
                MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues,
                PreviousLinesToFetch = 3,
                NextLinesToFetch = 200,
                MinimumSubMatches = 1,
                IncludeStartLabelText = true,
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
                        IncludeStartLabelText = true,
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
                                IncludeStartLabelText = true,
                                SubLabels = new List<LabelToMatch>
                                {
                                    new()
                                    {
                                        Name = "DateOnly",
                                        Text = [
                                            new("Up to and including "),
                                            new("From "),
                                            new("aggregate quantity of water authorised")
                                        ],
                                        IgnoreBlockIfContains = [
                                            "Note:"
                                        ],
                                        Position = LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
                                        Format = "Date",
                                        IncludeStartLabelText = true,
                                        MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues
                                    },
                                    new()
                                    {
                                        Name = "DatePurposeRough",
                                        Format = "Text",
                                        TextStart = [
                                            new("January") { LineMustStartWith = true },
                                            new("February") { LineMustStartWith = true },
                                            new("March") { LineMustStartWith = true },
                                            new("April") { LineMustStartWith = true },
                                            new("May") { LineMustStartWith = true },
                                            new("June") { LineMustStartWith = true },
                                            new("July") { LineMustStartWith = true },
                                            new("August") { LineMustStartWith = true },
                                            new("September") { LineMustStartWith = true },
                                            new("October") { LineMustStartWith = true },
                                            new("November") { LineMustStartWith = true }
                                        ],
                                        TextEnd = [
                                            new("February[END_OF_COLUMN]"),
                                            new("March[END_OF_COLUMN]"),
                                            new("April[END_OF_COLUMN]"),
                                            new("May[END_OF_COLUMN]"),
                                            new("June[END_OF_COLUMN]"),
                                            new("July[END_OF_COLUMN]"),
                                            new("August[END_OF_COLUMN]"),
                                            new("September[END_OF_COLUMN]"),
                                            new("October[END_OF_COLUMN]"),
                                            new("November[END_OF_COLUMN]"),
                                            new("December[END_OF_COLUMN]")
                                        ],
                                        PreviousLinesToFetch = 0,
                                        NextLinesToFetch = 0,
                                        Position = LabelPosition.TextToFindIsBetweenLabels,
                                        IncludeStartLabelText = true,
                                        IncludeEndLabelText = true,
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
                                        IncludeStartLabelText = true,
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
                                        Format = LicenceNumber.Constant,
                                        MultipleBehaviour = MultipleBehaviour.FindSingleInstanceOfLabelWithMultipleValues,
                                        SkipLineWhenContains = [
                                            new("Licence Serial No: ")
                                        ]
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
                                            "thousand cubic metres",
                                            "cubic metres",
                                            "cubic meters",
                                            "m\u00b3", // m3
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        },
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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
                                            "thousand cubic metres",
                                            "cubic metres",
                                            "cubic meters",
                                            "m\u00b3", // m3
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        },
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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
                                            "thousand cubic metres",
                                            "cubic metres",
                                            "cubic meters",
                                            "m\u00b3", // m3
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        },
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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
                                            "thousand cubic metres",
                                            "cubic metres",
                                            "cubic meters",
                                            "m\u00b3", // m3
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        },
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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
                                            "thousand cubic metres",
                                            "cubic metres",
                                            "cubic meters",
                                            "m\u00b3", // m3
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        },
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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
                                            "thousand cubic metres",
                                            "cubic metres",
                                            "cubic meters",
                                            "m\u00b3", // m3
                                            "megagallons",
                                            "thousand gallons",
                                            "million gallons",
                                            "gallons"                                    
                                        },
                                        SkipLineWhenContains = [
                                            "abstracted in total"
                                        ],
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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
                                        ],
                                        MultipleBehaviour = MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                                        FindMultipleOnSingleLine = true
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