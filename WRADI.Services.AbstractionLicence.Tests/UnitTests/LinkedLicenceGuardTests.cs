using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class LinkedLicenceGuardTests
{
    [Fact]
    public void WhenAnAggregateLinksToItsOwnLicence_ThenItThrows()
    {
        var licence = LicenceBuilder.A()
            .Numbered("1/2/3")
            .Holding(AggregateBuilder.An().For("1/2/3").LinkedTo("1/2/3").Build())
            .Build();

        Assert.Throws<Exception>(() =>
            LinkedLicenceAssertions.AssertOrFixNoLicenceIsLinkedToItself(
                [licence],
                fixWhenDetected: false));
    }

    [Fact]
    public void WhenFixingIsAskedFor_ThenTheSelfLinkIsStrippedAndTheOthersKept()
    {
        var aggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("1/2/3", "4/5/6").Build();
        var licence = LicenceBuilder.A().Numbered("1/2/3").Holding(aggregate).Build();

        LinkedLicenceAssertions.AssertOrFixNoLicenceIsLinkedToItself(
            [licence],
            fixWhenDetected: true);

        Assert.Equal("4/5/6", Assert.Single(aggregate.LinkedLicences!));
    }

    [Fact]
    public void WhenTheAggregateIsSourcedFromAnotherLicence_ThenTheSelfLinkIsNotLookedAt()
    {
        var licence = LicenceBuilder.A()
            .Numbered("1/2/3")
            .Holding(AggregateBuilder.An().For("9/9/9").LinkedTo("1/2/3").Build())
            .Build();

        LinkedLicenceAssertions.AssertOrFixNoLicenceIsLinkedToItself(
            [licence],
            fixWhenDetected: false);
    }

    [Theory]
    [InlineData(ScrapeStatus.Error)]
    [InlineData(ScrapeStatus.TransientError)]
    public void WhenTheLicenceDidNotExtractCleanly_ThenItsSelfLinkIsIgnored(ScrapeStatus status)
    {
        var licence = LicenceBuilder.A()
            .Numbered("1/2/3")
            .ThatDidNotExtractCleanly(status)
            .Holding(AggregateBuilder.An().For("1/2/3").LinkedTo("1/2/3").Build())
            .Build();

        LinkedLicenceAssertions.AssertOrFixNoLicenceIsLinkedToItself(
            [licence],
            fixWhenDetected: false);
    }

    [Fact]
    public void WhenTheLicenceHasNoNumber_ThenItsSelfLinkCannotBeDetected()
    {
        var licence = LicenceBuilder.A()
            .Holding(AggregateBuilder.An().For("1/2/3").LinkedTo("1/2/3").Build())
            .Build();

        LinkedLicenceAssertions.AssertOrFixNoLicenceIsLinkedToItself(
            [licence],
            fixWhenDetected: false);
    }

    [Fact]
    public void WhenASetNamesALicenceTheLinksDoNotCover_ThenItThrows()
    {
        var licence = LicenceBuilder.A().Numbered("1/2/3").InLicenceSets("123-456").Build();

        Assert.Throws<Exception>(() =>
            LinkedLicenceAssertions.AssertNoLicenceSetWithoutLinkedLicence([licence]));
    }

    [Fact]
    public void WhenTheOnlyPartOfTheSetIdIsTheLicenceItself_ThenNoLinkIsNeeded()
    {
        var licence = LicenceBuilder.A().Numbered("1/2/3").InLicenceSets("123").Build();

        LinkedLicenceAssertions.AssertNoLicenceSetWithoutLinkedLicence([licence]);
    }

    // Dots, dashes, slashes and spaces come off both sides before they are compared.
    [Fact]
    public void WhenTheNumbersAreWrittenWithDifferentSeparators_ThenTheyStillMatch()
    {
        var licence = LicenceBuilder.A()
            .Numbered("1/2/3")
            .InLicenceSets("123-456")
            .LinkedTo(LicenceBuilder.LinkTo("4.5 6"))
            .Build();

        LinkedLicenceAssertions.AssertNoLicenceSetWithoutLinkedLicence([licence]);
    }

    // LicenceSetReference.LicenceSetId is nullable and read with a null-forgiving operator.
    [Fact]
    public void WhenASetReferenceHasNoId_ThenItThrowsANullReference()
    {
        var licence = LicenceBuilder.A().Numbered("1/2/3").InLicenceSets([null]).Build();

        Assert.Throws<NullReferenceException>(() =>
            LinkedLicenceAssertions.AssertNoLicenceSetWithoutLinkedLicence([licence]));
    }

    [Fact]
    public void WhenFixingASetWithAMissingLink_ThenTheLinkIsBuiltFromTheOtherLicence()
    {
        var licence = LicenceBuilder.A().Numbered("1/2/3").InLicenceSets("123-456").Build();

        var other = LicenceBuilder.A().Numbered("4/5/6").Build();
        other.RegionId = 3;
        other.Filename = "4-5-6.pdf";

        LinkedLicenceAssertions.FixNoLicenceSetWithoutLinkedLicence([licence, other]);

        var added = Assert.Single(licence.LinkedLicences);
        Assert.Equal("4/5/6", added.LicenceNumber);
        Assert.Equal(3, added.RegionId);
        Assert.Equal("4-5-6.pdf", added.Filename);
        Assert.Equal(InformationDirection.InSameGroup, added.ContainedIn![0].Direction);
        Assert.Equal(InformationSource.Unknown, added.ContainedIn![0].Source);
    }

    [Fact]
    public void WhenTheNamedLicenceIsNotAmongTheLicences_ThenFixingThrows()
    {
        var licence = LicenceBuilder.A().Numbered("1/2/3").InLicenceSets("123-456").Build();

        Assert.Throws<Exception>(() =>
            LinkedLicenceAssertions.FixNoLicenceSetWithoutLinkedLicence([licence]));
    }

    [Fact]
    public void WhenTheSameLinkedLicenceAppearsTwice_ThenItThrows()
    {
        Assert.Throws<Exception>(() =>
            LinkedLicenceAssertions.AssertNoDuplicateLinkedLicences(["1/2/3", "1/2/3"]));
    }

    [Fact]
    public void WhenTheLinkedLicencesAreDistinct_ThenItPasses()
    {
        LinkedLicenceAssertions.AssertNoDuplicateLinkedLicences(["1/2/3", "4/5/6"]);
    }

    // The duplicate check compares the numbers as written, so the same licence written two ways
    // is not seen as a duplicate - the opposite of the set check above.
    [Fact]
    public void WhenTheSameLicenceIsWrittenTwoWays_ThenItIsNotSeenAsADuplicate()
    {
        LinkedLicenceAssertions.AssertNoDuplicateLinkedLicences(["1/2/3", "1 2 3"]);
    }
}
