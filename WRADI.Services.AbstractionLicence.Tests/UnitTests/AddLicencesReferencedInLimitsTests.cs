using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.DocumentType.AbstractionLicence.Enums;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class AddLicencesReferencedInLimitsTests
{
    [Fact]
    public void WhenALicenceIsNamedInTheLimitsButMissingFromTheSet_ThenItIsAdded()
    {
        var primary = LicenceBuilder.A()
            .Numbered("1/2/3")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6", [InLimits()]))
            .Build();

        var other = LicenceBuilder.A().Numbered("4/5/6").Build();
        var limitsSet = LimitsSetOf(primary);

        AbstractionLimitHelper.AddLicencesReferencedInLimitsToLicenceSets(
            [limitsSet], primary, [primary, other], [limitsSet]);

        Assert.Equal(["1/2/3", "4/5/6"], limitsSet.Licences.Select(l => l.LicenceNumber?.Value));
    }

    [Fact]
    public void WhenTheLicenceIsAlreadyInTheSet_ThenNothingChanges()
    {
        var primary = LicenceBuilder.A()
            .Numbered("1/2/3")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6", [InLimits()]))
            .Build();

        var other = LicenceBuilder.A().Numbered("4/5/6").Build();
        var limitsSet = LimitsSetOf(primary, other);

        AbstractionLimitHelper.AddLicencesReferencedInLimitsToLicenceSets(
            [limitsSet], primary, [primary, other], [limitsSet]);

        Assert.Equal(2, limitsSet.Licences.Length);
    }

    // The gate is only a count: what the explicitly-referenced sets contain is never read.
    [Fact]
    public void WhenNothingWasExplicitlyReferenced_ThenNothingIsAdded()
    {
        var primary = LicenceBuilder.A()
            .Numbered("1/2/3")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6", [InLimits()]))
            .Build();

        var limitsSet = LimitsSetOf(primary);

        AbstractionLimitHelper.AddLicencesReferencedInLimitsToLicenceSets(
            [limitsSet], primary, [primary, LicenceBuilder.A().Numbered("4/5/6").Build()], []);

        Assert.Single(limitsSet.Licences);
    }

    [Fact]
    public void WhenThereIsNoLimitsGroupedSet_ThenNothingIsAdded()
    {
        var primary = LicenceBuilder.A()
            .Numbered("1/2/3")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6", [InLimits()]))
            .Build();

        var lineageSet = LicenceSetBuilder.A()
            .OfType(LicenceSetType.LicencesGroupedByLineage)
            .Containing(primary)
            .Build();

        AbstractionLimitHelper.AddLicencesReferencedInLimitsToLicenceSets(
            [lineageSet], primary, [primary, LicenceBuilder.A().Numbered("4/5/6").Build()], [lineageSet]);

        Assert.Single(lineageSet.Licences);
    }

    [Fact]
    public void WhenTheLinkCameFromAnotherSection_ThenItIsNotAdded()
    {
        var primary = LicenceBuilder.A()
            .Numbered("1/2/3")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6", [new ContainedInInformation { SectionName = "Purposes" }]))
            .Build();

        var limitsSet = LimitsSetOf(primary);

        AbstractionLimitHelper.AddLicencesReferencedInLimitsToLicenceSets(
            [limitsSet], primary, [primary, LicenceBuilder.A().Numbered("4/5/6").Build()], [limitsSet]);

        Assert.Single(limitsSet.Licences);
    }

    // IsBecauseOfAggregate is tested inside Any over ContainedIn, so a link carrying no entries at
    // all is skipped however it arose.
    [Fact]
    public void WhenAnAggregateLinkHasNoContainedInEntries_ThenItIsNotAdded()
    {
        var link = LicenceBuilder.LinkTo("4/5/6");
        link.IsBecauseOfAggregate = true;
        link.ContainedIn = [];

        var primary = LicenceBuilder.A().Numbered("1/2/3").LinkedTo(link).Build();
        var limitsSet = LimitsSetOf(primary);

        AbstractionLimitHelper.AddLicencesReferencedInLimitsToLicenceSets(
            [limitsSet], primary, [primary, LicenceBuilder.A().Numbered("4/5/6").Build()], [limitsSet]);

        Assert.Single(limitsSet.Licences);
    }

    [Fact]
    public void WhenTheNamedLicenceIsNotAmongTheLicences_ThenItIsSkipped()
    {
        var primary = LicenceBuilder.A()
            .Numbered("1/2/3")
            .LinkedTo(LicenceBuilder.LinkTo("9/9/9", [InLimits()]))
            .Build();

        var limitsSet = LimitsSetOf(primary);

        AbstractionLimitHelper.AddLicencesReferencedInLimitsToLicenceSets(
            [limitsSet], primary, [primary], [limitsSet]);

        Assert.Single(limitsSet.Licences);
    }

    // Everything missing goes to the first limits set, which the code notes it cannot choose
    // between.
    [Fact]
    public void WhenThereAreSeveralLimitsSets_ThenTheFirstTakesThemAll()
    {
        var primary = LicenceBuilder.A()
            .Numbered("1/2/3")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6", [InLimits()]))
            .Build();

        var other = LicenceBuilder.A().Numbered("4/5/6").Build();
        var first = LimitsSetOf(primary);
        var second = LimitsSetOf(LicenceBuilder.A().Numbered("7/8/9").Build());

        AbstractionLimitHelper.AddLicencesReferencedInLimitsToLicenceSets(
            [first, second], primary, [primary, other], [first]);

        Assert.Equal(2, first.Licences.Length);
        Assert.Single(second.Licences);
    }

    [Fact]
    public void WhenTheSameLicenceIsNamedTwice_ThenTheDuplicateAssertionThrows()
    {
        var primary = LicenceBuilder.A()
            .Numbered("1/2/3")
            .LinkedTo(
                LicenceBuilder.LinkTo("4/5/6", [InLimits()]),
                LicenceBuilder.LinkTo("4/5/6", [InLimits()]))
            .Build();

        var limitsSet = LimitsSetOf(primary);

        Assert.Throws<Exception>(() =>
            AbstractionLimitHelper.AddLicencesReferencedInLimitsToLicenceSets(
                [limitsSet], primary, [primary, LicenceBuilder.A().Numbered("4/5/6").Build()], [limitsSet]));
    }

    private static LicenceSet LimitsSetOf(params Licence[] licences)
    {
        return LicenceSetBuilder.A()
            .OfType(LicenceSetType.LicencesGroupedByAbstractionLimits)
            .Containing(licences)
            .Build();
    }

    private static ContainedInInformation InLimits()
    {
        return new ContainedInInformation { SectionName = DocumentSectionNames.AbstractionLimits };
    }
}
