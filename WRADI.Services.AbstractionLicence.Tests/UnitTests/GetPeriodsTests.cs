using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class GetPeriodsTests
{
    [Fact]
    public void WhenThereIsNoPeriodsSection_ThenThereAreNoPeriods()
    {
        var data = Bag();

        Assert.Empty(LimitPeriodHelper.GetPeriods(
            [new LabelGroupResult { LabelGroupName = "SomethingElse" }], null, ref data));
    }

    // The NALD periods are recorded with Add, so the same dictionary cannot be used twice.
    [Fact]
    public void TheNaldPeriodsArePutInTheBagEvenWhenNothingIsFound()
    {
        var data = Bag();

        LimitPeriodHelper.GetPeriods([], null, ref data);

        Assert.True(data.ContainsKey("NaldPeriodsData"));
        Assert.Throws<ArgumentException>(() =>
            LimitPeriodHelper.GetPeriods([], null, ref data));
    }

    [Fact]
    public void WhenAPeriodNamesBothEnds_ThenTheyAreSplitOnTo()
    {
        var data = Bag();

        var periods = LimitPeriodHelper.GetPeriods(
            [Section(Period("1", "From 1 April to 31 October"))], null, ref data);

        var period = Assert.Single(periods);
        Assert.Equal("1", period.Id);
        Assert.Equal("1 April", period.StartDate);
        Assert.Equal("31 October", period.EndDate);
        Assert.Equal(AbstractionPeriodType.SetPeriod, period.PeriodType);
    }

    // "All year" has to match exactly - anything else is read as a set period.
    [Theory]
    [InlineData("All year", AbstractionPeriodType.PerYear)]
    [InlineData("all year", AbstractionPeriodType.SetPeriod)]
    [InlineData("All year round", AbstractionPeriodType.SetPeriod)]
    public void AllYearIsRecognisedOnlyWhenWrittenExactly(string text, AbstractionPeriodType expected)
    {
        var data = Bag();

        var periods = LimitPeriodHelper.GetPeriods(
            [Section(Period("1", text))], null, ref data);

        Assert.Equal(expected, Assert.Single(periods).PeriodType);
    }

    // Inclusive is detected without regard to case, but the word is only stripped out of the dates
    // when it is lower case - so a capitalised one stays in the end date.
    [Fact]
    public void WhenInclusiveIsCapitalised_ThenItIsDetectedButNotRemoved()
    {
        var data = Bag();

        var periods = LimitPeriodHelper.GetPeriods(
            [Section(Period("1", "1 April to 31 October Inclusive"))], null, ref data);

        var period = Assert.Single(periods);
        Assert.True(period.Inclusive);
        Assert.Equal("31 October Inclusive", period.EndDate);
    }

    [Fact]
    public void WhenInclusiveIsLowerCase_ThenItIsRemovedFromTheDates()
    {
        var data = Bag();

        var periods = LimitPeriodHelper.GetPeriods(
            [Section(Period("1", "1 April to 31 October inclusive"))], null, ref data);

        Assert.Equal("31 October", Assert.Single(periods).EndDate);
    }

    // An "Up to and Including" line becomes a cutoff, but it also stays in the description and in
    // the text the dates are read from.
    [Fact]
    public void AnUpToAndIncludingLineBecomesACutoffAndRemainsInTheText()
    {
        var data = Bag();

        var periods = LimitPeriodHelper.GetPeriods(
            [Section(Period("1", "1 April to 31 October", "Up to and Including 2030"))], null, ref data);

        var period = Assert.Single(periods);
        Assert.Equal(CutoffType.Upto, period.TimeCutoff!.CutoffType);
        Assert.Equal("2030", period.TimeCutoff.Date);
        Assert.Contains("Up to and Including 2030", period.Description);
    }

    [Fact]
    public void WhenAPeriodHasNeitherNumberNorText_ThenItIsSkipped()
    {
        var data = Bag();

        Assert.Empty(LimitPeriodHelper.GetPeriods(
            [Section(new LabelGroupResult())], null, ref data));
    }

    // Points and purposes are left unset, which the method notes as still to do.
    [Fact]
    public void ThePointAndPurposeIdsAreNeverSet()
    {
        var data = Bag();

        var period = Assert.Single(LimitPeriodHelper.GetPeriods(
            [Section(Period("1", "1 April to 31 October"))], null, ref data));

        Assert.Null(period.PointIds);
        Assert.Null(period.PurposeIds);
    }

    // The NALD dates come from the section's own text, not from each period, so every period on a
    // licence reports the same pair.
    [Fact]
    public void EveryPeriodCarriesTheSameNaldDates()
    {
        var data = Bag();
        var nald = new NaldAbstractionData
        {
            Periods = [new NaldDataPeriod { PeriodStartDay = 1, PeriodStartMonth = 4, PeriodEndDay = 31, PeriodEndMonth = 10 }]
        };

        var periods = LimitPeriodHelper.GetPeriods(
            [Section(Period("1", "1 April to 31 October"), Period("2", "1 May to 30 September"))],
            nald,
            ref data);

        Assert.Equal(2, periods.Length);
        Assert.All(periods, p =>
        {
            Assert.Equal("1/4", p.NaldPeriodStart);
            Assert.Equal("31/10", p.NaldPeriodEnd);
        });
    }

    private static Dictionary<string, object?> Bag() => new();

    private static LabelGroupResult Section(params LabelGroupResult[] periods)
    {
        return new LabelGroupResult
        {
            LabelGroupName = "PeriodsOfAbstraction",
            Text = LabelGroupResultBuilder.TextToLines("periods"),
            SubResults = periods
        };
    }

    private static LabelGroupResult Period(string number, params string[] textLines)
    {
        return new LabelGroupResult
        {
            SubResults =
            [
                new LabelGroupResult { MatchedLabelName = "PeriodPeriodNumber", Text = LabelGroupResultBuilder.TextToLines(number) },
                new LabelGroupResult
                {
                    MatchedLabelName = "PeriodTextWithoutPurposeAndPoint",
                    Text = textLines.SelectMany(LabelGroupResultBuilder.TextToLines).ToList()
                }
            ]
        };
    }

}
