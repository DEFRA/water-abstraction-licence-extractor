using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Enums;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

public static class LimitPeriodHelper
{
    public static PeriodOfAbstraction[] GetPeriods(
        List<LabelGroupResult> matches,
        NaldAbstractionData? naldDataLine,
        ref Dictionary<string, object?> noneSchemaData)
    {
        noneSchemaData.Add("NaldPeriodsData", naldDataLine?.Periods ?? []);

        var periodResults = matches.FirstOrDefault(result => result.LabelGroupName == "PeriodsOfAbstraction");
        var returnList = new List<PeriodOfAbstraction>();

        if (periodResults == null)
        {
            return returnList.ToArray();
        }

        if (periodResults.MatchedLabelName == "DuringTheMonthsXToYOnlyText")
        {
            if (periodResults.SubResults.Count != 2)
            {
                return returnList.ToArray();
            }

            returnList.Add(new PeriodOfAbstraction
            {
                PeriodType = AbstractionPeriodType.SetPeriod,
                Description = periodResults.Text?.FirstOrDefault()?.Text,
                Inclusive = true,
                StartDate = periodResults.SubResults[0].Text?.FirstOrDefault()?.Text,
                EndDate = periodResults.SubResults[1].Text?.FirstOrDefault()?.Text,
                NaldPeriodStart = GetNaldPeriodStartDate(naldDataLine,
                    periodResults.Text?.FirstOrDefault()?.Text),
                NaldPeriodEnd = GetNaldPeriodEndDate(naldDataLine,
                    periodResults.Text?.FirstOrDefault()?.Text)
            });
        }

        foreach (var pointResult in periodResults.SubResults)
        {
            var periodPeriodNumber = pointResult.SubResults
                .FirstOrDefault(x => x.MatchedLabelName == "PeriodPeriodNumber");

            var textWithoutNumber = pointResult.SubResults
                .FirstOrDefault(x => x.MatchedLabelName == "PeriodTextWithoutPurposeAndPoint")?
                .Text?
                .Select(t => t.Text)
                .ToList();

            if (textWithoutNumber == null && periodPeriodNumber == null)
            {
                continue;
            }

            var tKey = "Up to and Including ";

            var allTextWithoutNumber = textWithoutNumber?
                .Where(t => !t.StartsWith(tKey, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (allTextWithoutNumber == null)
            {
                continue;
            }

            var upToAndIncludeLine = textWithoutNumber?
                .FirstOrDefault(t => t.StartsWith(tKey, StringComparison.OrdinalIgnoreCase));
            TimeCutoff? timeCutoff = null;

            if (upToAndIncludeLine != null)
            {
                var date = upToAndIncludeLine.Replace(tKey, string.Empty);

                timeCutoff = new TimeCutoff
                {
                    CutoffType = CutoffType.Upto,
                    Date = date
                };
            }

            var text = textWithoutNumber != null
                ? string.Join('\n', textWithoutNumber)
                : null;

            var number = periodPeriodNumber?.Text?.FirstOrDefault()?.Text;
            
            var inclusive = text?.Contains("inclusive",
                StringComparison.OrdinalIgnoreCase) ?? false;

            var allYear = text == "All year";

            // TODO next bit should be done in config
            var dateParts = text?
                .Replace("From", string.Empty)
                .Replace("inclusive", string.Empty)
                .Split(" to ");

            var startDate = dateParts?[0].Trim();
            var endDate = dateParts?.Length >= 2 ? dateParts[1].Trim() : null;

            returnList.Add(new PeriodOfAbstraction
            {
                Id = number,
                PeriodType = allYear ? AbstractionPeriodType.PerYear : AbstractionPeriodType.SetPeriod,
                Description = text,
                Inclusive = inclusive,
                StartDate = startDate,
                EndDate = endDate,
                TimeCutoff = timeCutoff,
                PointIds = null, // TODO set purpose ids and point ids
                PurposeIds = null, // TODO set purpose ids and point ids
                NaldPeriodStart = GetNaldPeriodStartDate(naldDataLine,
                    periodResults.Text?.FirstOrDefault()?.Text),
                NaldPeriodEnd = GetNaldPeriodEndDate(naldDataLine,
                    periodResults.Text?.FirstOrDefault()?.Text)
            });
        }

        return returnList.ToArray();
    }
    // One phrase, one reading. These two used to match the same text independently, so
    // "Up to and including 31 March" satisfied both: a period running from "Up" to
    // "and including 31 March", and a correct Upto cutoff.
    private enum PeriodShape
    {
        None,
        Range,
        UpperBound,
        LowerBound
    }

    private readonly record struct PeriodPhrase(PeriodShape Shape, string? Start, string? End);

    // Licences appear in mixed case and in full caps, and String.Split takes no comparison.
    private static string[] SplitOn(string text, string separator)
    {
        return Regex.Split(text, Regex.Escape(separator), RegexOptions.IgnoreCase)
            .Select(part => part.Trim())
            .ToArray();
    }

    private static string? AfterFirst(string text, string marker)
    {
        var at = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        return at < 0 ? null : OnlyTheDate(text[(at + marker.Length)..]);
    }

    // The date ends where the clause does, or the value runs on into the rest of the
    // sentence: "31 July, abstraction shall not exceed 60,000".
    private static string OnlyTheDate(string text)
    {
        var comma = text.IndexOf(',');

        return (comma < 0 ? text : text[..comma]).Trim(' ', ',', '.', ':', ';');
    }

    private static readonly string[] MonthNames =
    [
        "january", "february", "march", "april", "may", "june",
        "july", "august", "september", "october", "november", "december"
    ];

    // "as varied from time to time" splits on " to " into a perfectly well-formed range of
    // "time" and "time)". A date carries a day number, or is a bare month name.
    // Searching for a month anywhere instead reads "such date as the Agency may specify" as May.
    private static bool LooksLikeADate(string? text)
    {
        return !string.IsNullOrWhiteSpace(text)
               && (text.Any(char.IsDigit)
                   || MonthNames.Contains(text.Trim(), StringComparer.OrdinalIgnoreCase));
    }

    // One end of a range is often left open - "to the end of the season", "and such date as the
    // Agency may specify". The bound that does read is still worth keeping, as the boundary it is.
    private static PeriodPhrase Range(string? start, string? end)
    {
        if (LooksLikeADate(start) && LooksLikeADate(end))
        {
            return new PeriodPhrase(PeriodShape.Range, start, end);
        }

        if (LooksLikeADate(start))
        {
            return new PeriodPhrase(PeriodShape.LowerBound, start, null);
        }

        return LooksLikeADate(end)
            ? new PeriodPhrase(PeriodShape.UpperBound, null, end)
            : new PeriodPhrase(PeriodShape.None, null, null);
    }

    private static PeriodPhrase Classify(LabelGroupResult? match)
    {
        var value = match?.Text?.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(value))
        {
            return new PeriodPhrase(PeriodShape.None, null, null);
        }

        var work = value
            .Replace("inclusive", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim(' ', ',', '.', ':', ';');

        // Tested before the range split, which would otherwise read this as a range starting at "Up".
        if (AfterFirst(work, "Up to and including") is { Length: > 0 } upTo)
        {
            return new PeriodPhrase(PeriodShape.UpperBound, null, upTo);
        }

        if (AfterFirst(work, "Until ") is { Length: > 0 } until)
        {
            return new PeriodPhrase(PeriodShape.UpperBound, null, until);
        }

        // "Up to 31 March" without the "and including", a smaller share of the same fault.
        if (work.StartsWith("Up to ", StringComparison.OrdinalIgnoreCase)
            && AfterFirst(work, "Up to ") is { Length: > 0 } upToOnly)
        {
            return new PeriodPhrase(PeriodShape.UpperBound, null, upToOnly);
        }

        // The one range wording joined by "and" rather than "to".
        if (AfterFirst(work, "Between ") is { Length: > 0 } between)
        {
            var ends = SplitOn(between, " and ");

            if (ends.Length == 2)
            {
                return Range(OnlyTheDate(ends[0]), OnlyTheDate(ends[1]));
            }
        }

        var normalised = work
            .Replace(" and ending on ", " to ", StringComparison.OrdinalIgnoreCase)
            .Replace("beginning on ", string.Empty, StringComparison.OrdinalIgnoreCase);

        var parts = SplitOn(normalised, " to ");

        if (parts.Length >= 2)
        {
            return Range(AfterFirst(parts[0], "From ") ?? OnlyTheDate(parts[0]), OnlyTheDate(parts[1]));
        }

        return AfterFirst(work, "From ") is { Length: > 0 } onlyStart
            ? new PeriodPhrase(PeriodShape.LowerBound, onlyStart, null)
            : new PeriodPhrase(PeriodShape.None, null, null);
    }

    public static TimePeriod? GetTimePeriod(LabelGroupResult? match)
    {
        var phrase = Classify(match);

        return phrase.Shape != PeriodShape.Range
            ? null
            : new TimePeriod
            {
                StartDate = phrase.Start,
                EndDate = phrase.End,
                PeriodType = AbstractionPeriodType.SetPeriod,
                Inclusive = true
            };
    }

    public static TimeCutoff? GetTimeCutoff(LabelGroupResult? match)
    {
        var phrase = Classify(match);

        return phrase.Shape switch
        {
            PeriodShape.UpperBound => new TimeCutoff { CutoffType = CutoffType.Upto, Date = phrase.End },
            PeriodShape.LowerBound => new TimeCutoff { CutoffType = CutoffType.From, Date = phrase.Start },
            _ => null
        };
    }

    public static TimePeriod? GetDefinitionOfYear(List<LabelGroupResult> matches)
    {
        var abstractionLimitsSection = matches
            .FirstOrDefault(result => result.LabelGroupName == DocumentSectionNames.AbstractionLimits);

        var abstractionLimitPoints = abstractionLimitsSection?
            .SubResults
            .Where(res => res.MatchedLabelName == "AbstractionLimitPoint")
            .ToList();

        var abstractionLimitPointSubs = abstractionLimitPoints?
            .SelectMany(res => res.SubResults)
            .Where(res => res.MatchedLabelName == "AbstractionLimitPointSub")
            .ToList();

        if (abstractionLimitPointSubs != null)
        {
            foreach (var abstractionLimitPointSub in abstractionLimitPointSubs)
            {
                var definition = abstractionLimitPointSub.SubResults
                    .SingleOrDefault(sr => sr.MatchedLabelName == "AYearDefinitionLine");

                if (definition == null)
                {
                    continue;
                }

                var dates = definition.SubResults;
                var text = definition.Text?.FirstOrDefault()?.Text;
                var inclusive = text?.Contains("beginning on") == true
                                || text?.Contains("ending on") == true;

                return new TimePeriod
                {
                    PeriodType = AbstractionPeriodType.SetPeriod,
                    Inclusive = inclusive,
                    StartDate = dates.FirstOrDefault()?.Text?.FirstOrDefault()?.Text,
                    EndDate = dates.LastOrDefault()?.Text?.FirstOrDefault()?.Text
                };
            }
        }

        return null;
    }
    public static string? GetNaldPeriodStartDate(NaldAbstractionData? naldDataLine, string? description)
    {
        if (naldDataLine == null)
        {
        }

        if (naldDataLine?.Periods.Count is null or 0)
        {
            return null;
        }

        var periods = naldDataLine.Periods;
        var period = periods.Count == 1
            ? periods[0]
            : periods.First(p => p.PeriodStartDay != null);

        return $"{period.PeriodStartDay}/{period.PeriodStartMonth}";
    }
    public static string? GetNaldPeriodEndDate(NaldAbstractionData? naldDataLine, string? description)
    {
        if (naldDataLine?.Periods.Count is null or 0)
        {
            return null;
        }

        var periods = naldDataLine.Periods;
        var period = periods.Count == 1
            ? periods[0]
            : periods.First(p => p.PeriodEndDay != null);

        return $"{period.PeriodEndDay}/{period.PeriodEndMonth}";
    }
    public static LimitPeriodType ToLimitPeriodType(string? text)
    {
        return text?.ToLower() switch
        {
            "per second" => LimitPeriodType.PerSecond,
            "per minute" => LimitPeriodType.PerMinute,
            "per hour" => LimitPeriodType.PerHour,
            "per day" => LimitPeriodType.PerDay,
            "per week" => LimitPeriodType.PerWeek,
            "per month" => LimitPeriodType.PerMonth,
            "per annum" => LimitPeriodType.PerYear,
            "per year" => LimitPeriodType.PerYear,
            "aggregate annual abstraction" => LimitPeriodType.PerYear,
            "in total" => LimitPeriodType.InTotal,
            "total annual quantity" => LimitPeriodType.InTotal,
            "consecutive five year" => LimitPeriodType.Per5Years,
            "five consecutive years" => LimitPeriodType.Per5Years,
            "over any 5-year period" => LimitPeriodType.Per5Years,
            _ => throw new NotSupportedException($"Unknown limit period type '{text}'")
        };
    }
}
