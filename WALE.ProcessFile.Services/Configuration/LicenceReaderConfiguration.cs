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
}