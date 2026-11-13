using WALE.ProcessFile.Core.Constants;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class LinkReasonTests
{
    // In the order the branches are tested: the order is what settles a text carrying two of them.
    public static TheoryData<string, string> Phrases => new()
    {
        { "continuous release of water", LinkReason.ReleaseOfWater },
        { "quantity of water released", LinkReason.ReleaseOfWater },
        { "rate of flow", LinkReason.RateOfFlow },
        { "minimum value", LinkReason.MinimumValue },
        { "lapsed licence", LinkReason.LapsedLicence },
        { "discharge and re-abstraction", LinkReason.DischargeAndReabstractionCondition },
        { "simultaneous discharge", LinkReason.SimultaneousDischargeCondition },
        { "simultaneous abstraction", LinkReason.SimultaneousAbstractionCondition },
        { "simultaneous compensatory discharge", LinkReason.SimultaneousCompensatoryDischargeCondition },
        { "compensatory discharge", LinkReason.CompensatoryDischargeCondition },
        { "compensation flow", LinkReason.CompensationFlow },
        { "read in conjunction", LinkReason.ReadInConjunction },
        { "The donor licence was", LinkReason.DonorLicence },
        { "used in conjunction", LinkReason.UsedInConjunction },
        { "use in conjunction", LinkReason.UsedInConjunction },
        { "revocation", LinkReason.Revocation },
        { "when the flow", LinkReason.WhenTheFlow },
        { "aggregate conditions", LinkReason.AggregateConditions },
        { "emergency circumstances", LinkReason.EmergencyCircumstances },
        { "Dewatering Discharge", LinkReason.DewateringDischargeCondition },
        { "when added to", LinkReason.WhenAddedTo },
        { "subsequent abstraction", LinkReason.SubsequentAbstraction },
        { "re-abstraction", LinkReason.ReAbstraction },
        { "readings discharged augmentation", LinkReason.ReadingsDischargedAugmentationCondition },
        { "aggregate", LinkReason.AggregateCondition },
        { "in total between", LinkReason.AggregateCondition },
        { "in an emergency", LinkReason.InAnEmergency },
        { "shall not exceed", LinkReason.ShallNotExceed },
        { "supporting", LinkReason.Supporting },
        { "original licence", LinkReason.OriginalLicence },
        { "transferred to this", LinkReason.TransferredToThis },
        { "coincident", LinkReason.Coincident },
        { "shall be supported", LinkReason.ShallBeSupported },
        { "residual flow", LinkReason.ResidualFlow },
        { "authorised by", LinkReason.AuthorisedBy }
    };

    [Theory]
    [MemberData(nameof(Phrases))]
    public void WhenTheTextCarriesAPhrase_ThenItsReasonIsReturned(string phrase, string expected)
    {
        Assert.Equal(
            expected,
            LinkReasonHelper.GetLinkReason($"the licence says {phrase} somewhere", null));
    }

    [Fact]
    public void EveryLinkReasonButEmptyIsReachableFromSomePhrase()
    {
        var reached = Phrases
            .Select(row => (string)row[1])
            .Distinct()
            .OrderBy(reason => reason);

        var declared = LinkReason.GetAll()
            .Where(reason => reason != LinkReason.Empty)
            .OrderBy(reason => reason);

        Assert.Equal(declared, reached);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WhenThereIsNoText_ThenThereIsNoReason(string? text)
    {
        Assert.Null(LinkReasonHelper.GetLinkReason(text, null));
    }

    [Fact]
    public void WhenTheTextMatchesNoPhrase_ThenThereIsNoReason()
    {
        Assert.Null(LinkReasonHelper.GetLinkReason("nothing of interest here", null));
    }

    // textToFind gates the method rather than taking part in the matching.
    [Fact]
    public void WhenTheTextDoesNotCarryTheTextToFind_ThenNoPhraseIsLookedFor()
    {
        Assert.Null(LinkReasonHelper.GetLinkReason(
            "this text mentions revocation",
            "a phrase that is not there"));
    }

    [Fact]
    public void WhenTheTextCarriesTheTextToFind_ThenThePhrasesAreLookedFor()
    {
        Assert.Equal(
            LinkReason.Revocation,
            LinkReasonHelper.GetLinkReason(
                "this text mentions revocation",
                "mentions"));
    }

    [Fact]
    public void WhenThePhraseIsWrittenInAnotherCase_ThenItStillMatches()
    {
        Assert.Equal(
            LinkReason.AggregateCondition,
            LinkReasonHelper.GetLinkReason("AGGREGATE QUANTITY", null));
    }

    // The three phrases in the list that contain another one, so the only three whose order
    // against each other decides the answer.
    [Theory]
    [InlineData("simultaneous compensatory discharge", LinkReason.SimultaneousCompensatoryDischargeCondition)]
    [InlineData("aggregate conditions", LinkReason.AggregateConditions)]
    [InlineData("discharge and re-abstraction", LinkReason.DischargeAndReabstractionCondition)]
    public void WhenOnePhraseContainsAnother_ThenTheLongerOneIsTestedFirst(string text, string expected)
    {
        Assert.Equal(expected, LinkReasonHelper.GetLinkReason(text, null));
    }

    // "aggregate" is a bare word two thirds of the way down the list, so a clause that mentions
    // aggregation can never reach the ten phrases below it.
    [Fact]
    public void WhenTheTextMentionsAggregation_ThenTheLaterPhrasesCannotWin()
    {
        Assert.Equal(
            LinkReason.AggregateCondition,
            LinkReasonHelper.GetLinkReason(
                "the aggregate quantity shall not exceed 100 cubic metres",
                null));
    }

    // The only branch that needs more than one phrase, and it needs all three.
    [Fact]
    public void WhenOnlySomeOfTheAugmentationPhrasesArePresent_ThenThereIsNoReason()
    {
        Assert.Null(LinkReasonHelper.GetLinkReason(
            "readings taken of water discharged",
            null));
    }

    // The nine reasons IsExcludedLinkReason drops are also marked in LinkReason itself, so the
    // list exists twice and the two have to agree.
    [Fact]
    public void TheExcludedReasonsAreExactlyTheNineFlowAndDischargeOnes()
    {
        var excluded = LinkReason.GetAll()
            .Where(LinkReasonHelper.IsExcludedLinkReason)
            .OrderBy(reason => reason);

        Assert.Equal(
            new[]
            {
                LinkReason.CompensationFlow,
                LinkReason.CompensatoryDischargeCondition,
                LinkReason.DewateringDischargeCondition,
                LinkReason.MinimumValue,
                LinkReason.RateOfFlow,
                LinkReason.ReadingsDischargedAugmentationCondition,
                LinkReason.ReleaseOfWater,
                LinkReason.SimultaneousDischargeCondition,
                LinkReason.WhenTheFlow
            }.OrderBy(reason => reason),
            excluded);
    }

    [Fact]
    public void WhenThereIsNoReasonAtAll_ThenItIsNotExcluded()
    {
        Assert.False(LinkReasonHelper.IsExcludedLinkReason(null));
    }

    [Fact]
    public void WhenTheFirstSectionYieldsNothing_ThenTheNextOneIsRead()
    {
        Assert.Equal(
            LinkReason.Revocation,
            LinkReasonHelper.GetLinkReason(
                [
                    SectionBuilder.A().WithLines("nothing of interest").Build(),
                    SectionBuilder.A().WithLines("a revocation applies").Build()
                ],
                null));
    }

    [Fact]
    public void WhenASectionYieldsAReason_ThenTheLaterSectionsAreNotRead()
    {
        Assert.Equal(
            LinkReason.Revocation,
            LinkReasonHelper.GetLinkReason(
                [
                    SectionBuilder.A().WithLines("a revocation applies").Build(),
                    SectionBuilder.A().WithLines("read in conjunction with another").Build()
                ],
                null));
    }

    // A section's lines are joined with a newline rather than a space.
    [Fact]
    public void WhenThePhraseIsBrokenOverTwoLines_ThenItIsNotFound()
    {
        Assert.Null(LinkReasonHelper.GetLinkReason(
            [
                SectionBuilder.A()
                    .WithLines("this licence is to be used in", "conjunction with another")
                    .Build()
            ],
            null));
    }
}
