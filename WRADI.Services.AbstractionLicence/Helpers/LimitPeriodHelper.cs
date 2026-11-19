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
            //var id = double.TryParse(number, out var numberResult) ? numberResult : (double?)null;

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
    public static TimePeriod? GetTimePeriod(LabelGroupResult? match)
    {
        if (match == null)
        {
            return null;
        }

        var value = match.Text?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Replace(" and ending on ", " to ");

        var isFrom = value.Contains("From ", StringComparison.OrdinalIgnoreCase);
        var isUntil = value.Contains("Until ", StringComparison.OrdinalIgnoreCase);

        if (isFrom || isUntil)
        {
            return null;
        }

        var hasTo = value.Contains(" to ", StringComparison.OrdinalIgnoreCase);
        var hasBeginningOn = value.Contains("beginning on ", StringComparison.OrdinalIgnoreCase);

        if (!hasTo && !hasBeginningOn)
        {
            return null;
        }

        var parts = value
            .Replace("beginning on ", string.Empty)
            .Split(" to ");

        if (parts.Length == 1)
        {
            return null;
        }

        return new TimePeriod
        {
            StartDate = parts[0],
            EndDate = parts.Length > 1 ? parts[1] : null,
            PeriodType = AbstractionPeriodType.SetPeriod,
            Inclusive = true
        };
    }
    public static TimeCutoff? GetTimeCutoff(LabelGroupResult? match)
    {
        if (match == null)
        {
            return null;
        }

        var value = match.Text?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var isFrom = value.Contains("From ", StringComparison.OrdinalIgnoreCase);
        var isUntil = value.Contains("Until ", StringComparison.OrdinalIgnoreCase)
                      || value.Contains("Up to and including", StringComparison.OrdinalIgnoreCase);

        if (!isFrom && !isUntil)
        {
            return null;
        }

        var parts = value
            .Replace("From", "~", StringComparison.OrdinalIgnoreCase)
            .Replace("Until", "~", StringComparison.OrdinalIgnoreCase)
            .Replace("Up to and including", "~", StringComparison.OrdinalIgnoreCase)
            .Split('~');

        var datePart = parts.Length >= 2 ? parts[1] : null;

        if (datePart == null)
        {
            return null;
        }

        return new TimeCutoff
        {
            CutoffType = isFrom ? CutoffType.From : CutoffType.Upto,
            Date = datePart.Trim()
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
