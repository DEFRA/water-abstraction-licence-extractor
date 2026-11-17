using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.DocumentType.AbstractionLicence.Converters;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

// GetTimePeriod and GetTimeCutoff read the same text and divide it between them: a phrase naming
// both ends is a period, one naming a single boundary is a cutoff.
public class TimePeriodAndCutoffTests
{
    [Fact]
    public void WhenBothEndsAreNamed_ThenItIsASetPeriod()
    {
        var period = AbstractionLicenceSchemaConverter.GetTimePeriod(Match("1 April to 31 March"));

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
        var period = AbstractionLicenceSchemaConverter.GetTimePeriod(Match(text));

        Assert.NotNull(period);
        Assert.Equal("1 April", period.StartDate);
        Assert.Equal("31 March", period.EndDate);
    }

    // A one-sided phrase belongs to GetTimeCutoff, so the period parser declines it.
    [Theory]
    [InlineData("From 1 April")]
    [InlineData("Until 31 March")]
    [InlineData("beginning on 1 April")]
    [InlineData("1 April")]
    [InlineData("")]
    [InlineData("   ")]
    public void OneSidedOrUnparseableTextIsNotAPeriod(string text)
    {
        Assert.Null(AbstractionLicenceSchemaConverter.GetTimePeriod(Match(text)));
    }

    [Fact]
    public void WhenThereIsNoMatch_ThenThereIsNoPeriodAndNoCutoff()
    {
        Assert.Null(AbstractionLicenceSchemaConverter.GetTimePeriod(null));
        Assert.Null(AbstractionLicenceSchemaConverter.GetTimeCutoff(null));
    }

    // "From" and "Until" on a two-ended phrase win over the period reading, so such text yields
    // neither a period nor a sensible cutoff end.
    [Fact]
    public void WhenAFromPhraseAlsoNamesAnEnd_ThenOnlyTheStartSurvives()
    {
        Assert.Null(AbstractionLicenceSchemaConverter.GetTimePeriod(Match("From 1 April to 31 March")));

        var cutoff = AbstractionLicenceSchemaConverter.GetTimeCutoff(Match("From 1 April to 31 March"));

        Assert.NotNull(cutoff);
        Assert.Equal(CutoffType.From, cutoff.CutoffType);
        Assert.Equal("1 April to 31 March", cutoff.Date);
    }

    [Theory]
    [InlineData("From 1 April", CutoffType.From, "1 April")]
    [InlineData("Until 31 March", CutoffType.Upto, "31 March")]
    [InlineData("Up to and including 31 March", CutoffType.Upto, "31 March")]
    public void ASingleBoundaryBecomesACutoff(string text, CutoffType expectedType, string expectedDate)
    {
        var cutoff = AbstractionLicenceSchemaConverter.GetTimeCutoff(Match(text));

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
        Assert.Null(AbstractionLicenceSchemaConverter.GetTimeCutoff(Match(text)));
    }

    private static LabelGroupResult Match(string text)
    {
        return new LabelGroupResult
        {
            Text =
            [
                new DocumentLine
                {
                    Columns =
                    [
                        new DocumentLineColumn
                        {
                            Words = text.Split(' ')
                                .Select(word => new DocumentLineWord { Text = word })
                                .ToList()
                        }
                    ]
                }
            ]
        };
    }
}
