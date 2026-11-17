using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Core.Tests.UnitTests;

/// <summary>
/// StripForComparison decides whether two licence numbers refer to the same licence, under
/// linked-licence resolution, aggregate matching and the NALD cross-check. These state current
/// behaviour, not desired behaviour.
/// </summary>
public class FormattingHelperStripForComparisonTests
{
    private const int Anglian = 1;
    private const int NorthEast = 3;
    private const int Thames = 7;

    [Theory]
    [InlineData("4/29/010/174")]
    [InlineData("4.29.010.174")]
    [InlineData("4 29 010 174")]
    [InlineData("4-29-010-174")]
    [InlineData("4//29/010/174")]
    public void WhenSeparatorsDiffer_ThenTheSameLicenceCompactsIdentically(string licenceNumber)
    {
        Assert.Equal("4_29_10_174", FormattingHelper.StripForComparison(licenceNumber, Anglian));
    }

    [Fact]
    public void WhenAPartHasLeadingZeros_ThenTheyAreStripped()
    {
        Assert.Equal("4_29_10_174", FormattingHelper.StripForComparison("4/29/010/174", Anglian));
    }

    // Two numbers differing only by a leading zero are the same licence in Anglian, different
    // licences in Thames.
    [Fact]
    public void WhenRegionIsThames_ThenLeadingZerosAreKept()
    {
        Assert.Equal("4_29_010_174", FormattingHelper.StripForComparison("4/29/010/174", Thames));

        Assert.NotEqual(
            FormattingHelper.StripForComparison("4/29/010/174", Thames),
            FormattingHelper.StripForComparison("4/29/10/174", Thames));

        Assert.Equal(
            FormattingHelper.StripForComparison("4/29/010/174", Anglian),
            FormattingHelper.StripForComparison("4/29/10/174", Anglian));
    }

    // A 1/2x/... number takes the NE path from its shape alone, before IsNeLicenceNumber falls
    // back to the region argument - and that path keeps leading zeros, hence 04 rather than 4.
    [Theory]
    [InlineData(Anglian)]
    [InlineData(NorthEast)]
    [InlineData(Thames)]
    public void WhenTheNumberLooksNorthEast_ThenTheRegionArgumentDoesNotChangeIt(int regionCode)
    {
        Assert.Equal("1_25_04_174", FormattingHelper.StripForComparison("1/25/04/174", regionCode));
    }

    // Both pairs are named in the blocks commented out inside the method: the removed
    // trailing-zero and all-zero handling made each pair compare equal.
    [Theory]
    [InlineData("4/29/10/*G/0010", "4/29/10/*G/0100")]
    [InlineData("6/33/02/*G/0103", "6/33/02/*G/0013")]
    public void WhenTwoNumbersDifferOnlyByZeroPlacement_ThenTheyStayDistinct(
        string licenceNumber1,
        string licenceNumber2)
    {
        Assert.NotEqual(
            FormattingHelper.StripForComparison(licenceNumber1, Anglian),
            FormattingHelper.StripForComparison(licenceNumber2, Anglian));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WhenThereIsNoLicenceNumber_ThenTheResultIsNull(string? licenceNumber)
    {
        Assert.Null(FormattingHelper.StripForComparison(licenceNumber, NorthEast));
    }

    // Known defect, not desired behaviour: IsNeLicenceNumber reads licenceNumber[1] with no
    // length check. Zero instances in the finder data, so not raised as one.
    [Theory]
    [InlineData("1")]
    [InlineData("X")]
    public void WhenTheLicenceNumberIsOneCharacter_ThenItThrows(string licenceNumber)
    {
        Assert.Throws<IndexOutOfRangeException>(
            () => FormattingHelper.StripForComparison(licenceNumber, NorthEast));
    }
}
