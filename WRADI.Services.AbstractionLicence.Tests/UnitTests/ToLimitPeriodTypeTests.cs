using WRADI.Core.AbstractionLicence.Enums;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class ToLimitPeriodTypeTests
{
    [Theory]
    [InlineData("per second", LimitPeriodType.PerSecond)]
    [InlineData("per minute", LimitPeriodType.PerMinute)]
    [InlineData("per hour", LimitPeriodType.PerHour)]
    [InlineData("per day", LimitPeriodType.PerDay)]
    [InlineData("per week", LimitPeriodType.PerWeek)]
    [InlineData("per month", LimitPeriodType.PerMonth)]
    [InlineData("per annum", LimitPeriodType.PerYear)]
    [InlineData("per year", LimitPeriodType.PerYear)]
    [InlineData("aggregate annual abstraction", LimitPeriodType.PerYear)]
    [InlineData("in total", LimitPeriodType.InTotal)]
    [InlineData("total annual quantity", LimitPeriodType.InTotal)]
    [InlineData("consecutive five year", LimitPeriodType.Per5Years)]
    [InlineData("five consecutive years", LimitPeriodType.Per5Years)]
    [InlineData("over any 5-year period", LimitPeriodType.Per5Years)]
    public void EachKnownPhraseMapsToItsPeriod(string text, LimitPeriodType expected)
    {
        Assert.Equal(expected, LimitPeriodHelper.ToLimitPeriodType(text));
    }

    [Theory]
    [InlineData("PER DAY")]
    [InlineData("Per Day")]
    [InlineData("pEr DaY")]
    public void TheMatchIgnoresCase(string text)
    {
        Assert.Equal(LimitPeriodType.PerDay, LimitPeriodHelper.ToLimitPeriodType(text));
    }

    // Only the exact phrase matches - no trimming, so whitespace from the document is fatal.
    [Theory]
    [InlineData(" per day")]
    [InlineData("per day ")]
    [InlineData("perday")]
    [InlineData("daily")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseThrows(string? text)
    {
        Assert.Throws<NotSupportedException>(() => LimitPeriodHelper.ToLimitPeriodType(text));
    }

    // Unknown and NotApplicable are set elsewhere rather than matched from the document, so no
    // phrase produces them.
    [Fact]
    public void EveryOtherPeriodTypeIsReachableFromSomePhrase()
    {
        var reached = Enum.GetValues<LimitPeriodType>()
            .Where(periodType =>
            {
                try
                {
                    return Phrases.Any(p =>
                        LimitPeriodHelper.ToLimitPeriodType(p) == periodType);
                }
                catch (NotSupportedException)
                {
                    return false;
                }
            })
            .ToList();

        var unreachable = Enum.GetValues<LimitPeriodType>().Except(reached).ToList();

        Assert.Equal([LimitPeriodType.Unknown, LimitPeriodType.NotApplicable], unreachable);
    }

    private static readonly string[] Phrases =
    [
        "per second", "per minute", "per hour", "per day", "per week", "per month", "per annum",
        "per year", "aggregate annual abstraction", "in total", "total annual quantity",
        "consecutive five year", "five consecutive years", "over any 5-year period"
    ];
}
