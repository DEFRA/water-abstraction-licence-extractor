using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

// GetTimePeriod and GetTimeCutoff read the same text and divide it between them: a phrase naming
// both ends is a period, one naming a single boundary is a cutoff.
public class TimePeriodAndCutoffTests
{
    [Fact]
    public void WhenBothEndsAreNamed_ThenItIsASetPeriod()
    {
        var period = LimitPeriodHelper.GetTimePeriod(Match("1 April to 31 March"));

        Assert.NotNull(period);
        Assert.Equal("1 April", period.StartDate);
        Assert.Equal("31 March", period.EndDate);
        Assert.Equal(AbstractionPeriodType.SetPeriod, period.PeriodType);
        Assert.True(period.Inclusive);
    }

    [Theory]
    [InlineData("1 April and ending on 31 March")]
    [InlineData("beginning on 1 April to 31 March")]
    public void TheWordingIsNormalisedBeforeSplitting(string text)
    {
        var period = LimitPeriodHelper.GetTimePeriod(Match(text));

        Assert.NotNull(period);
        Assert.Equal("1 April", period.StartDate);
        Assert.Equal("31 March", period.EndDate);
    }

    [Theory]
    [InlineData("From 1 April")]
    [InlineData("Until 31 March")]
    [InlineData("Up to and including 31 March 2038")]
    [InlineData("beginning on 1 April")]
    [InlineData("1 April")]
    [InlineData("")]
    [InlineData("   ")]
    public void OneSidedOrUnparseableTextIsNotAPeriod(string text)
    {
        Assert.Null(LimitPeriodHelper.GetTimePeriod(Match(text)));
    }

    [Fact]
    public void WhenThereIsNoMatch_ThenThereIsNoPeriodAndNoCutoff()
    {
        Assert.Null(LimitPeriodHelper.GetTimePeriod(null));
        Assert.Null(LimitPeriodHelper.GetTimeCutoff(null));
    }

    [Theory]
    [InlineData("From 1 April to 31 October inclusive", "1 April", "31 October")]
    [InlineData("From 1 April to 30 September inclusive", "1 April", "30 September")]
    [InlineData("From 1 November to 31 March inclusive", "1 November", "31 March")]
    public void WhenAFromPhraseAlsoNamesAnEnd_ThenItIsARangeAndNotACutoff(
        string text,
        string expectedStart,
        string expectedEnd)
    {
        var period = LimitPeriodHelper.GetTimePeriod(Match(text));

        Assert.NotNull(period);
        Assert.Equal(expectedStart, period.StartDate);
        Assert.Equal(expectedEnd, period.EndDate);
        Assert.Null(LimitPeriodHelper.GetTimeCutoff(Match(text)));
    }

    [Theory]
    [InlineData("Between 1 June and 31 July inclusive", "1 June", "31 July")]
    [InlineData("Between 1 May and 30 September inclusive", "1 May", "30 September")]
    [InlineData("Between 1 November in any one year and 31 March in the following year",
        "1 November in any one year", "31 March in the following year")]
    public void ABetweenPhraseIsARange(string text, string expectedStart, string expectedEnd)
    {
        var period = LimitPeriodHelper.GetTimePeriod(Match(text));

        Assert.NotNull(period);
        Assert.Equal(expectedStart, period.StartDate);
        Assert.Equal(expectedEnd, period.EndDate);
        Assert.Null(LimitPeriodHelper.GetTimeCutoff(Match(text)));
    }

    [Theory]
    [InlineData("Up to and including 31 March 2038")]
    [InlineData("Up to 31 March 2027")]
    [InlineData("For the purpose of trickle irrigation up to and including 31 March 2025")]
    public void AnUpperBoundIsACutoffAndNotAPeriod(string text)
    {
        Assert.Null(LimitPeriodHelper.GetTimePeriod(Match(text)));

        var cutoff = LimitPeriodHelper.GetTimeCutoff(Match(text));

        Assert.NotNull(cutoff);
        Assert.Equal(CutoffType.Upto, cutoff.CutoffType);
        Assert.DoesNotContain(" to ", cutoff.Date);
    }

    [Theory]
    [InlineData("as varied from time to time")]
    [InlineData("(from time to time) for the purpose")]
    [InlineData("subject to the provisions")]
    public void ProseIsNotARange(string text)
    {
        Assert.Null(LimitPeriodHelper.GetTimePeriod(Match(text)));
    }

    [Fact]
    public void AMonthWithoutADayIsStillADate()
    {
        var period = LimitPeriodHelper.GetTimePeriod(Match("From April to October inclusive"));

        Assert.NotNull(period);
        Assert.Equal("April", period.StartDate);
        Assert.Equal("October", period.EndDate);
    }

    [Fact]
    public void ADateDoesNotRunOnIntoTheRestOfTheClause()
    {
        var period = LimitPeriodHelper.GetTimePeriod(
            Match("Between 01 June and 31 July inclusive, abstraction shall not exceed 60,000"));

        Assert.NotNull(period);
        Assert.Equal("01 June", period.StartDate);
        Assert.Equal("31 July", period.EndDate);
    }

    [Fact]
    public void ACutoffDateDoesNotRunOnIntoTheRestOfTheClause()
    {
        var cutoff = LimitPeriodHelper.GetTimeCutoff(
            Match("Up to and including 31 March 2027, the aggregate quantity of water"));

        Assert.NotNull(cutoff);
        Assert.Equal("31 March 2027", cutoff.Date);
    }

    [Theory]
    [InlineData("From 1 April to 31 October inclusive")]
    [InlineData("Between 1 June and 31 July inclusive")]
    [InlineData("Up to and including 31 March 2038")]
    public void ACutoffDateNeverHoldsARange(string text)
    {
        var date = LimitPeriodHelper.GetTimeCutoff(Match(text))?.Date;

        Assert.DoesNotContain(" to ", date ?? string.Empty);
        Assert.DoesNotContain(" and ", date ?? string.Empty);
    }

    [Theory]
    [InlineData("From 1 April", CutoffType.From, "1 April")]
    [InlineData("Until 31 March", CutoffType.Upto, "31 March")]
    [InlineData("Up to and including 31 March", CutoffType.Upto, "31 March")]
    public void ASingleBoundaryBecomesACutoff(string text, CutoffType expectedType, string expectedDate)
    {
        var cutoff = LimitPeriodHelper.GetTimeCutoff(Match(text));

        Assert.NotNull(cutoff);
        Assert.Equal(expectedType, cutoff.CutoffType);
        Assert.Equal(expectedDate, cutoff.Date);
    }

    [Theory]
    [InlineData("1 April to 31 March")]
    [InlineData("beginning on 1 April")]
    [InlineData("")]
    public void TextNamingNoBoundaryIsNotACutoff(string text)
    {
        Assert.Null(LimitPeriodHelper.GetTimeCutoff(Match(text)));
    }

    [Theory]
    [InlineData("between 1 June and 31 July inclusive")]
    [InlineData("BETWEEN 1 JUNE AND 31 JULY INCLUSIVE")]
    public void TheWordingIsMatchedWhateverItsCase(string text)
    {
        Assert.NotNull(LimitPeriodHelper.GetTimePeriod(Match(text)));
    }

    // Both ends have to read as dates to make a range, but the one that does is kept as the
    // boundary it is rather than discarded with the other.
    [Theory]
    [InlineData("From 1 April to the end of the season")]
    [InlineData("Between 1 April and such date as the Agency may specify")]
    public void ARangeWithAVagueEndKeepsTheStartAsALowerBound(string text)
    {
        Assert.Null(LimitPeriodHelper.GetTimePeriod(Match(text)));

        var cutoff = LimitPeriodHelper.GetTimeCutoff(Match(text));

        Assert.NotNull(cutoff);
        Assert.Equal(CutoffType.From, cutoff.CutoffType);
        Assert.Equal("1 April", cutoff.Date);
    }

    [Fact]
    public void ARangeWithAVagueStartKeepsTheEndAsAnUpperBound()
    {
        var cutoff = LimitPeriodHelper.GetTimeCutoff(
            Match("From such date as the Agency may specify to 31 October"));

        Assert.NotNull(cutoff);
        Assert.Equal(CutoffType.Upto, cutoff.CutoffType);
        Assert.Equal("31 October", cutoff.Date);
    }

    // Neither end readable is still nothing
    [Fact]
    public void ARangeWithNeitherEndReadableIsNothing()
    {
        Assert.Null(LimitPeriodHelper.GetTimePeriod(Match("as varied from time to time")));
        Assert.Null(LimitPeriodHelper.GetTimeCutoff(Match("as varied from time to time")));
    }

    [Fact]
    public void ABetweenPhraseNamingThreeThingsIsNotARange()
    {
        Assert.Null(LimitPeriodHelper.GetTimePeriod(
            Match("Between 1 April and 30 June and 1 September")));
    }

    [Theory]
    [InlineData("1 April to 31 March")]
    [InlineData("From 1 April to 31 March inclusive")]
    public void ARangeIsInclusiveWhetherOrNotTheWordAppears(string text)
    {
        Assert.True(LimitPeriodHelper.GetTimePeriod(Match(text))?.Inclusive);
    }

    [Fact]
    public void ADateIsNotTrimmedWhereTheClauseHasNoComma()
    {
        var cutoff = LimitPeriodHelper.GetTimeCutoff(
            Match("Up to and including 31 March 2027 for the purpose of spray irrigation"));

        Assert.Equal("31 March 2027 for the purpose of spray irrigation", cutoff?.Date);
    }

    private static LabelGroupResult Match(string text)
    {
        return LabelGroupResultBuilder.A().Saying(text).Build();
    }
}
