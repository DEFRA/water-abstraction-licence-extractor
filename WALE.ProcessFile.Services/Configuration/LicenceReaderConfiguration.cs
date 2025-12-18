using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums;
using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Configuration;

public static class LicenceReaderConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            ("Company", GetCompanyNameLabels()),
            ("LicenceNumber", GetLicenceNumberLabels()),
            ("DateOfIssue", GetDateOfIssueLabels()),
            ("Licence Header", GetHeaderLabels()),
            ("Addendum", GetAddendumLabels()),
            ("Issuer", GetIssuerLabels()),
            ("Variation", GetVariationLabels()),
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
                    new("Water Resources Act 1991 as amended by")
                    {
                        IsRegularExpression = true
                    },
                    new("Yorkshire.* River Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*Yorkshire.* Water Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*Northumbrian.* River.* Authority")
                    {
                    IsRegularExpression = true
                    },
                    new(".*Northumbrian.* Water.* Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*National.* River.* Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*National.* Water.* Authority")
                    {
                        IsRegularExpression = true
                    },
                    new(".*Environment.* Agency")
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
                    new("Please keep this addendum with"),
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ApplicableToMost,
                IncludeStartLabelText = true
            }
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
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ApplicableToMost,
                IncludeStartLabelText = true
            }
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
                ],
                IgnoreMatchIfContains = [
                    "Date effective"
                ],
            },
            new LabelToMatch
            {
                Name = "DateOfIssueOldStyle",
                Format = "Text",
                Text =
                [
                    new("'DATED THIS") { ColumnMustStartWith = true },
                    new("DATED THIS") { ColumnMustStartWith = true },
                    new("DATE THIS") { ColumnMustStartWith = true }
                ],
                Remove = [
                    new("DATED THIS"),
                    new("DATE THIS")
                ],
                MustContain = [
                    new("Jan"),
                    new("Feb"),
                    new("Mar"),
                    new("Apr"),
                    new("May"),
                    new("Nay"), //Misreading
                    new("Hay"), //Misreading                    
                    new("Jun"),
                    new("Jul"),
                    new("Aug"),
                    new("Sep"),
                    new("Oct"),
                    new("Nov"),
                    new("Dec")
                ],
                IgnoreMatchIfContains = [
                    "Date effective"
                ],
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Position = LabelPosition.ApplicableToMost
            }
        ];
    }
    
    public static List<LabelToMatch> GetLicenceNumberLabels()
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
}