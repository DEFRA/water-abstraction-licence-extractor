using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Configuration;

public static class SharedLabels
{
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
                MultipleServiceMatchBehaviour = MultipleServiceMatchBehaviour.UseFirstServiceResult,
                Format = LicenceNumber.Constant,
                Name = "DocumentLicenceNumber",
                PreviousLinesToFetch = 2,
                NextLinesToFetch = 1
            },
            new LabelToMatch
            {
                Text =
                [
                    new("Hampshire Ref")
                ],
                Position = LabelPosition.LabelIsAfterTextToFind,
                Format = LicenceNumber.Constant,
                Name = "DocumentLicenceNumberHampshire",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0
            }
        ];
    }
    
    public static List<LabelToMatch> GetDateOfIssueLabels()
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
                MultipleServiceMatchBehaviour = MultipleServiceMatchBehaviour.UseFullestDateUseLastServiceResultIfMultipleFull,
                Remove = [
                    new("...")
                ],
                IgnoreMatchIfContains = [
                    "Date effective"
                ]
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
                Position = LabelPosition.ApplicableToMost,
                MultipleServiceMatchBehaviour = MultipleServiceMatchBehaviour.UseFullestDateUseLastServiceResultIfMultipleFull
            }
        ];
    }
}