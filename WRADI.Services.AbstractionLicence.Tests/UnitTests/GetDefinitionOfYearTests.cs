using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.DocumentType.AbstractionLicence.Enums;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

// The definition is found four levels down: the AbstractionLimits group, its AbstractionLimitPoint
// results, their AbstractionLimitPointSub results, and an AYearDefinitionLine under one of those.
public class GetDefinitionOfYearTests
{
    [Fact]
    public void WhenTheDefinitionNamesBothDates_ThenTheyBecomeTheStartAndEnd()
    {
        var matches = Section(PointSub(Definition("beginning on 1 April", "1 April", "31 March")));

        var period = LimitPeriodHelper.GetDefinitionOfYear(matches);

        Assert.NotNull(period);
        Assert.Equal("1 April", period.StartDate);
        Assert.Equal("31 March", period.EndDate);
        Assert.Equal(AbstractionPeriodType.SetPeriod, period.PeriodType);
    }

    [Theory]
    [InlineData("beginning on 1 April", true)]
    [InlineData("ending on 31 March", true)]
    [InlineData("the year 1 April to 31 March", false)]
    public void InclusiveComesFromTheWording(string text, bool expected)
    {
        var matches = Section(PointSub(Definition(text, "1 April", "31 March")));

        Assert.Equal(expected, LimitPeriodHelper.GetDefinitionOfYear(matches)!.Inclusive);
    }

    // The wording test is case sensitive, unlike the same check in GetTimePeriod.
    [Fact]
    public void WhenTheWordingIsCapitalised_ThenItIsNotTreatedAsInclusive()
    {
        var matches = Section(PointSub(Definition("Beginning on 1 April", "1 April", "31 March")));

        Assert.False(LimitPeriodHelper.GetDefinitionOfYear(matches)!.Inclusive);
    }

    // Start is the first date result and end is the last, so a definition carrying one date
    // reports it as both ends of the year.
    [Fact]
    public void WhenOnlyOneDateIsFound_ThenItIsBothTheStartAndTheEnd()
    {
        var matches = Section(PointSub(Definition("beginning on 1 April", "1 April")));

        var period = LimitPeriodHelper.GetDefinitionOfYear(matches);

        Assert.Equal("1 April", period!.StartDate);
        Assert.Equal("1 April", period.EndDate);
    }

    [Fact]
    public void WhenTheDefinitionCarriesNoDates_ThenBothEndsAreMissing()
    {
        var matches = Section(PointSub(Definition("beginning on 1 April")));

        var period = LimitPeriodHelper.GetDefinitionOfYear(matches);

        Assert.NotNull(period);
        Assert.Null(period.StartDate);
        Assert.Null(period.EndDate);
    }

    [Fact]
    public void TheFirstPointSubHoldingADefinitionWins()
    {
        var matches = Section(
            PointSub(),
            PointSub(Definition("beginning on 1 April", "1 April", "31 March")),
            PointSub(Definition("beginning on 1 May", "1 May", "30 April")));

        Assert.Equal("1 April", LimitPeriodHelper.GetDefinitionOfYear(matches)!.StartDate);
    }

    [Theory]
    [InlineData("no abstraction limits section")]
    [InlineData("no definition")]
    public void WhenThereIsNothingToFind_ThenThereIsNoDefinition(string shape)
    {
        var matches = shape == "no definition"
            ? Section(PointSub())
            : [new LabelGroupResult { LabelGroupName = "SomethingElse" }];

        Assert.Null(LimitPeriodHelper.GetDefinitionOfYear(matches));
    }

    // The definition is looked up with SingleOrDefault, so two of them under one point sub throw
    // rather than the first winning.
    [Fact]
    public void WhenOnePointSubHasTwoDefinitions_ThenItThrows()
    {
        var matches = Section(PointSub(
            Definition("beginning on 1 April", "1 April", "31 March"),
            Definition("beginning on 1 May", "1 May", "30 April")));

        Assert.Throws<InvalidOperationException>(() =>
            LimitPeriodHelper.GetDefinitionOfYear(matches));
    }

    private static List<LabelGroupResult> Section(params LabelGroupResult[] pointSubs)
    {
        return
        [
            new LabelGroupResult
            {
                LabelGroupName = DocumentSectionNames.AbstractionLimits,
                SubResults =
                [
                    new LabelGroupResult
                    {
                        MatchedLabelName = "AbstractionLimitPoint",
                        SubResults = pointSubs
                    }
                ]
            }
        ];
    }

    private static LabelGroupResult PointSub(params LabelGroupResult[] definitions)
    {
        return new LabelGroupResult
        {
            MatchedLabelName = "AbstractionLimitPointSub",
            SubResults = definitions
        };
    }

    private static LabelGroupResult Definition(string text, params string[] dates)
    {
        return new LabelGroupResult
        {
            MatchedLabelName = "AYearDefinitionLine",
            Text = LabelGroupResultBuilder.TextToLines(text),
            SubResults = dates
                .Select(date => new LabelGroupResult { Text = LabelGroupResultBuilder.TextToLines(date) })
                .ToList()
        };
    }

}
