using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// LicenceNumberHelper.ContainsOther is what links a document to its linked licences. These state
/// current behaviour, not desired behaviour.
/// </summary>
public class LicenceNumberHelperContainsOtherTests
{
    private const int Anglian = 1;

    [Theory]
    [InlineData("4/29/10/174", "4/29/10/174")]
    [InlineData("4/29/10/174", "4/29/10")]
    [InlineData("4/29/10", "4/29/10/174")]
    public void WhenOneNumberContainsTheOther_ThenTheyAreTreatedAsTheSameLicence(
        string licenceNumber1,
        string licenceNumber2)
    {
        Assert.True(
            LicenceNumberHelper.ContainsOther(
                licenceNumber1, licenceNumber2, Anglian));
    }

    [Theory]
    [InlineData("4/29/10/174", "4/29/10/174/R01")]
    [InlineData("4/29/10/174/R01", "4/29/10/174")]
    public void WhenOnlyOneSideIsARevision_ThenTheyAreNotTheSameLicence(
        string licenceNumber1,
        string licenceNumber2)
    {
        Assert.False(
            LicenceNumberHelper.ContainsOther(
                licenceNumber1, licenceNumber2, Anglian));
    }

    [Fact]
    public void WhenBothSidesAreTheSameRevision_ThenTheyAreTheSameLicence()
    {
        Assert.True(
            LicenceNumberHelper.ContainsOther(
                "4/29/10/174/R01", "4/29/10/174/R01", Anglian));
    }

    // Questionable: both spellings count as revision markers for the rule above, but the
    // comparison is a substring test and StripForComparison leaves R01 alone while 010 becomes
    // 10. Zero affected licences in the finder data, so not raised as a defect.
    [Fact]
    public void WhenTheSameRevisionIsWrittenR1AndR01_ThenTheyDoNotMatch()
    {
        Assert.False(
            LicenceNumberHelper.ContainsOther(
                "4/29/10/174/R1", "4/29/10/174/R01", Anglian));
    }

    [Fact]
    public void WhenNeitherContainsTheOther_ThenTheyAreDifferentLicences()
    {
        Assert.False(
            LicenceNumberHelper.ContainsOther(
                "4/29/10/174", "5/29/10/174", Anglian));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WhenOneSideIsMissing_ThenThereIsNoMatch(string? missing)
    {
        Assert.False(
            LicenceNumberHelper.ContainsOther(
                "4/29/10/174", missing, Anglian));
        Assert.False(
            LicenceNumberHelper.ContainsOther(
                missing, "4/29/10/174", Anglian));
    }
}
