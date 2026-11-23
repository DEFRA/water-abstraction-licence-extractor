using WALE.ProcessFile.Core.Models.Nald;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class FollowOnContainedInTests
{
    [Fact]
    public void WhenThePredecessorsHistoryNamesTheSuccessor_ThenItsProvenanceIsReSourcedAsFollowOn()
    {
        var predecessor = Predecessor(SeenIn(InformationSource.Document, "Quantity"), successor: "2/27/18/123/R01");
        var successor = LicenceBuilder.LinkTo("2/27/18/123/R01", []);

        AbstractionLicenceSchemaConverter.AddFollowOnContainedIns(predecessor, successor);

        var carried = Assert.Single(successor.ContainedIn!);
        Assert.Equal(InformationSource.DocumentFollowOn, carried.Source);
        Assert.Equal("Quantity", carried.SectionName);
    }

    [Fact]
    public void WhenTheHistoryDoesNotNameTheSuccessor_ThenTheProvenanceIsCarriedUnchanged()
    {
        var predecessor = Predecessor(SeenIn(InformationSource.Document, "Quantity"), successor: "9/9/9");
        var successor = LicenceBuilder.LinkTo("2/27/18/123/R01", []);

        AbstractionLicenceSchemaConverter.AddFollowOnContainedIns(predecessor, successor);

        Assert.Equal(InformationSource.Document, Assert.Single(successor.ContainedIn!).Source);
    }

    [Fact]
    public void WhenTheSuccessorAlreadyHasItsOwnProvenance_ThenBothAreKept()
    {
        var predecessor = Predecessor(SeenIn(InformationSource.Document, "Quantity"), successor: "2/27/18/123/R01");
        var successor = LicenceBuilder.LinkTo("2/27/18/123/R01", [SeenIn(InformationSource.Nald, "Succession")]);

        AbstractionLicenceSchemaConverter.AddFollowOnContainedIns(predecessor, successor);

        Assert.Equal(2, successor.ContainedIn!.Length);
        Assert.Contains(successor.ContainedIn!, ci => ci.Source == InformationSource.Nald);
        Assert.Contains(successor.ContainedIn!, ci => ci.Source == InformationSource.DocumentFollowOn);
    }

    [Fact]
    public void WhenTwoPredecessorsCarryTheSameProvenance_ThenTheSuccessorHoldsItOnce()
    {
        var successor = LicenceBuilder.LinkTo("2/27/18/123/R01", []);

        foreach (var sectionName in new[] { "Quantity", "Quantity" })
        {
            AbstractionLicenceSchemaConverter.AddFollowOnContainedIns(
                Predecessor(SeenIn(InformationSource.Document, sectionName), successor: "2/27/18/123/R01"),
                successor);
        }

        Assert.Single(successor.ContainedIn!);
    }

    [Fact]
    public void WhenTwoPredecessorsCarryDifferentProvenance_ThenTheSuccessorHoldsBoth()
    {
        var successor = LicenceBuilder.LinkTo("2/27/18/123/R01", []);

        foreach (var sectionName in new[] { "Quantity", "Conditions" })
        {
            AbstractionLicenceSchemaConverter.AddFollowOnContainedIns(
                Predecessor(SeenIn(InformationSource.Document, sectionName), successor: "2/27/18/123/R01"),
                successor);
        }

        Assert.Equal(2, successor.ContainedIn!.Length);
    }

    [Fact]
    public void WhenThePredecessorHasNoProvenance_ThenTheSuccessorKeepsItsOwn()
    {
        var predecessor = LicenceBuilder.LinkTo("2/27/18/123", []);
        var successor = LicenceBuilder.LinkTo("2/27/18/123/R01", [SeenIn(InformationSource.Nald, "Succession")]);

        AbstractionLicenceSchemaConverter.AddFollowOnContainedIns(predecessor, successor);

        Assert.Equal(InformationSource.Nald, Assert.Single(successor.ContainedIn!).Source);
    }

    private static ContainedInInformation SeenIn(InformationSource source, string sectionName)
    {
        return new ContainedInInformation { Source = source, SectionName = sectionName };
    }

    private static LinkedLicence Predecessor(ContainedInInformation containedIn, string successor)
    {
        containedIn.History =
        [
            new NaldLicenceNumberHistoryOutput { FollowOnLicenceNumbers = [successor] }
        ];

        return LicenceBuilder.LinkTo("2/27/18/123", [containedIn]);
    }
}
