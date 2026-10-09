using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WALE.Tools._2ndHalf.Configuration;

/// <summary>
/// WRADI-415. Label rules for the three dates a WQ permit notice carries, which together let the
/// latest permit in DMS be identified by comparison with the ReSP version date:
///
/// 1. The latest date in the "Status log of the permit" table.
/// 2. The effective start date, from "The notice shall take effect from ...".
/// 3. The authorised date, recorded against the Environment Agency authoriser.
///
/// Kept apart from <see cref="WqFormLabelConfiguration"/>, which is about the Dry Weather Flow
/// clause replacement - the two concerns share documents but nothing else.
///
/// Scored by WqDateExtractionAccuracyTests, so a rule change here should be judged by that number
/// rather than by eye.
/// </summary>
public static class WqFormDateLabelConfiguration
{
    public const string StatusLogLabelGroup = "StatusLog";
    public const string EffectiveStartDateLabelGroup = "EffectiveStartDate";
    public const string EffectiveOnIssueLabelGroup = "EffectiveOnIssue";
    public const string AuthorisedDateLabelGroup = "AuthorisedDate";

    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels()
    {
        return
        [
            (StatusLogLabelGroup, GetStatusLogLabels()),
            (EffectiveStartDateLabelGroup, GetEffectiveStartDateLabels()),
            (EffectiveOnIssueLabelGroup, GetEffectiveOnIssueLabels()),
            (AuthorisedDateLabelGroup, GetAuthorisedDateLabels())
        ];
    }

    /// <summary>
    /// Captures the status log table as a block of lines, for the caller to pull dates out of and
    /// take the latest.
    ///
    /// The rows are NOT in date order - roughly one document in ten lists them out of sequence - so
    /// the latest date is the maximum of the column, never the last row. The table also repeats its
    /// heading as a continuation header when it spans a page break, so every instance is matched and
    /// the dates pooled.
    ///
    /// "End of introductory note" closes the table in most templates. The line cap is the backstop
    /// for the ones where it is absent, since over-running into the permit body would pull in future
    /// dates such as a review or expiry date.
    /// </summary>
    private static List<LabelToMatch> GetStatusLogLabels()
    {
        return
        [
            new LabelToMatch
            {
                TextStart =
                [
                    new("Status log of the permit"),
                    new("Status log of permit")
                ],
                TextEnd =
                [
                    new("End of introductory note")
                ],
                Position = LabelPosition.TextToFindIsBetweenLabels,
                IncludeWholeLine = true,
                Format = "Text",
                PreviousLinesToFetch = 0,
                // The heading-to-terminator distance is a median of 20 lines but reaches 58 when the
                // table spans a page break and carries a footer, so the cap is set well clear of
                // that. It is only a backstop for templates missing the terminator; WqDateExtraction
                // AccuracyTests flags any document whose status log date postdates its authorisation,
                // which is what over-capture would look like.
                NextLinesToFetch = 70,
                CanGoOverPageBoundary = true,
                MultipleMatchBehaviour =
                    MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel,
                Name = "StatusLogBlock"
            }
        ];
    }

    /// <summary>
    /// The notice states its own commencement in a sentence, which is the most explicit date in the
    /// document. The label deliberately stops at "take effect" rather than including "from", because
    /// a minority of the corpus omits that word ("The notice shall take effect 15/09/2025") and the
    /// date is read out of whatever follows either way.
    ///
    /// Three date forms appear after it, so the caller must parse all of them: "15/09/2025" in the
    /// bulk of the corpus, "28 July 2015" in a few, and the older "the 1st day of April 2009" in
    /// rather more than the written form.
    /// </summary>
    private static List<LabelToMatch> GetEffectiveStartDateLabels()
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
                Name = "EffectiveStartDateFromNoticeSentence"
            }
        ];
    }

    /// <summary>
    /// A minority of notices carry no commencement date at all: they say "The notice shall take
    /// effect from the date of issue." For those the effective start date IS the authorised date, so
    /// the absence of a date here is a statement rather than a gap. Matching the sentence lets the
    /// caller substitute it knowingly instead of recording the field as not found.
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
                Name = "EffectiveStartDateIsDateOfIssue"
            }
        ];
    }

    /// <summary>
    /// The authorisation block is a small table - a "Name / Date" header, the authoriser's name and
    /// date, then the words below it:
    /// <code>
    ///  Name                                   Date
    ///  Rob McHale                             15/09/2025
    ///
    /// Authorised on behalf of the Environment Agency
    /// </code>
    /// The date carries no label of its own, so the phrase underneath is the anchor and the date is
    /// read from the lines above it. The block repeats once per schedule in most documents, with the
    /// same date each time.
    /// </summary>
    private static List<LabelToMatch> GetAuthorisedDateLabels()
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
                Name = "AuthorisedDateFromSignatureBlock"
            }
        ];
    }
}
