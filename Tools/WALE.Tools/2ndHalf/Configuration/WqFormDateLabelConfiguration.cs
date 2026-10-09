using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WALE.Tools._2ndHalf.Configuration;

/// <summary>
/// Label rules for the two dates a WQ permit notice carries: when it takes effect and when it was
/// issued. Kept apart from <see cref="WqFormLabelConfiguration"/>, which is about the Dry Weather
/// Flow clause replacement - the two concerns share documents but nothing else.
///
/// Measured by WqDateExtractionAccuracyTests against the ground truth set, so a rule change here
/// should be judged by that score rather than by eye.
/// </summary>
public static class WqFormDateLabelConfiguration
{
    public const string EffectiveDateLabelGroup = "EffectiveDate";
    public const string IssuedDateLabelGroup = "IssuedDate";
    public const string EffectiveOnIssueLabelGroup = "EffectiveOnIssue";

    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            (EffectiveDateLabelGroup, GetEffectiveDateLabels()),
            (EffectiveOnIssueLabelGroup, GetEffectiveOnIssueLabels()),
            (IssuedDateLabelGroup, GetIssuedDateLabels())
        ];
    }

    /// <summary>
    /// The notice states its own commencement in a sentence, which is the most explicit date in the
    /// document: "The notice shall take effect from 15/09/2025". The label deliberately stops at
    /// "take effect" rather than including "from", because a minority of the corpus omits that word
    /// ("The notice shall take effect 15/09/2025") and the date is read out of what follows either
    /// way.
    /// </summary>
    private static List<LabelToMatch> GetEffectiveDateLabels()
    {
        return
        [
            new LabelToMatch
            {
                Text =
                [
                    new("The notice shall take effect"),
                    new("This notice shall take effect"),
                    new("The notice takes effect"),
                    new("shall take effect on")
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                Format = "Date",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 1,
                Name = "EffectiveDateFromNoticeSentence"
            }
        ];
    }

    /// <summary>
    /// A large minority of notices carry no commencement date at all: they say "The notice shall
    /// take effect from the date of issue." For those the effective date IS the issued date, so the
    /// absence of a date here is a statement rather than a gap. Matching the sentence lets the
    /// caller substitute the issued date knowingly, instead of recording the effective date as not
    /// found.
    /// </summary>
    private static List<LabelToMatch> GetEffectiveOnIssueLabels()
    {
        return
        [
            new LabelToMatch
            {
                Text =
                [
                    new("take effect from the date of issue"),
                    new("takes effect from the date of issue"),
                    new("take effect on the date of issue")
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                IncludeStartLabelText = true,
                IncludeWholeLine = true,
                Format = "Text",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                Name = "EffectiveDateIsDateOfIssue"
            }
        ];
    }

    /// <summary>
    /// The issue date is not labelled. It appears twice: as the date beside the signatory in the
    /// authorisation block, and as the date on the last row of the status log table. The signature
    /// block is preferred because the status log also holds every earlier determination, so picking
    /// the right row there needs the table structure rather than a label rule.
    ///
    /// "Variation determined" is included as a fallback for the templates whose signature block has
    /// no date beside the name.
    /// </summary>
    private static List<LabelToMatch> GetIssuedDateLabels()
    {
        return
        [
            new LabelToMatch
            {
                Text =
                [
                    new("Authorised on behalf of the Environment Agency"),
                    new("Authorised on behalf of the")
                ],
                Position = LabelPosition.LabelIsAfterTextToFind,
                Format = "Date",
                PreviousLinesToFetch = 4,
                NextLinesToFetch = 0,
                Name = "IssuedDateFromSignatureBlock"
            },
            new LabelToMatch
            {
                Text =
                [
                    new("Variation determined"),
                    new("Permit determined")
                ],
                Position = LabelPosition.LabelIsBeforeTextToFind,
                Format = "Date",
                PreviousLinesToFetch = 0,
                NextLinesToFetch = 0,
                MultipleMatchBehaviour =
                    MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                Name = "IssuedDateFromStatusLog"
            }
        ];
    }
}
