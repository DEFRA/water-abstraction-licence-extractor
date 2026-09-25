using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Models.Configuration;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// WrFluentRule accumulates into private fields and only assembles the real LabelToMatch in
/// Build() (see the class's own comment) - every modifier below is checked against the exact
/// property it's supposed to land on, rather than trusted by inspection. This is the gap that
/// let an earlier rewrite silently drop BoundByOtherLabels' wiring (the builder still had the
/// method and the backing field, Build() just stopped reading it into
/// LimitToBoundSameLineWalkByOtherLabelPositions) while leaving no failing test behind.
/// </summary>
public class WrFluentRuleTests
{
    [Fact]
    public void Between_SetsTextStartWithColumnMustStartWith()
    {
        var label = WrFluentRule.Between("Meter make", "Reading:").Build();

        var start = Assert.Single(label.TextStart!);
        Assert.Equal("Meter make", start.Text);
        Assert.True(start.ColumnMustStartWith);
    }

    [Fact]
    public void Between_SetsTextEndToTheEndTextThenTheEndOfBlockSentinel()
    {
        var label = WrFluentRule.Between("Meter make", "Reading:").Build();

        Assert.Collection(label.TextEnd!,
            t => { Assert.Equal("Reading:", t.Text); Assert.True(t.LineMustStartWith); },
            t => Assert.Equal("[END_OF_BLOCK]", t.Text));
    }

    [Fact]
    public void Between_SetsPositionToTextToFindIsBetweenLabels()
    {
        var label = WrFluentRule.Between("Meter make", "Reading:").Build();

        Assert.Equal(LabelPosition.TextToFindIsBetweenLabels, label.Position);
    }

    [Fact]
    public void Between_AddsTheStartTextToRemove()
    {
        var label = WrFluentRule.Between("Meter make", "Reading:").Build();

        Assert.Contains(label.Remove!, r => r.Text == "Meter make");
    }

    [Fact]
    public void After_SetsTextStartWithColumnMustStartWith_AndNoTextEnd()
    {
        var label = WrFluentRule.After("Inspecting Officer").Build();

        var start = Assert.Single(label.TextStart!);
        Assert.Equal("Inspecting Officer", start.Text);
        Assert.True(start.ColumnMustStartWith);
        Assert.Null(label.TextEnd);
    }

    [Fact]
    public void After_SetsPositionToLabelIsBeforeTextToFind()
    {
        var label = WrFluentRule.After("Inspecting Officer").Build();

        Assert.Equal(LabelPosition.LabelIsBeforeTextToFind, label.Position);
    }

    [Fact]
    public void After_AddsTheMatchedTextToRemove()
    {
        var label = WrFluentRule.After("Inspecting Officer").Build();

        Assert.Contains(label.Remove!, r => r.Text == "Inspecting Officer");
    }

    [Fact]
    public void InOrder_SetsTextStartToBothTheSpacedAndUnspacedForm()
    {
        var label = WrFluentRule.InOrder("Source of supply", null, "Quantities").Build();

        Assert.Equal(["Source of supply", "Sourceofsupply"], label.TextStart!.Select(t => t.Text));
    }

    [Fact]
    public void InOrder_DefaultsNextLinesToFetchToOne()
    {
        var label = WrFluentRule.InOrder("Source of supply", null, "Quantities").Build();

        Assert.Equal(1, label.NextLinesToFetch);
    }

    [Fact]
    public void InOrder_SetsPossibilitiesToTheSuppliedList()
    {
        var possibilities = new List<TextToMatch> { new("✓"), new("X") };

        var label = WrFluentRule.InOrder("Source of supply", possibilities, "Quantities").Build();

        Assert.Same(possibilities, label.Possibilities);
    }

    [Fact]
    public void InOrder_WithNoEndText_StillTerminatesAtEndOfBlock()
    {
        var label = WrFluentRule.InOrder("Special conditions", null).Build();

        var end = Assert.Single(label.TextEnd!);
        Assert.Equal("[END_OF_BLOCK]", end.Text);
    }

    [Fact]
    public void Named_SetsName()
    {
        var label = WrFluentRule.After("Time").Named("Time").Build();

        Assert.Equal("Time", label.Name);
    }

    [Fact]
    public void NextLines_SetsNextLinesToFetch()
    {
        var label = WrFluentRule.Between("A", "B").NextLines(10).Build();

        Assert.Equal(10, label.NextLinesToFetch);
    }

    [Fact]
    public void RequireTextToClaimGroup_SetsRequireTextToBePresent()
    {
        var label = WrFluentRule.After("Reading:").RequireTextToClaimGroup().Build();

        Assert.True(label.RequireTextToBePresent);
    }

    [Fact]
    public void WithoutRequireTextToClaimGroup_RequireTextToBePresentDefaultsFalse()
    {
        var label = WrFluentRule.After("Reading:").Build();

        Assert.False(label.RequireTextToBePresent);
    }

    [Fact]
    public void RequireCompleteDateToClaimGroup_SetsTheFlag()
    {
        var label = WrFluentRule.Between("Inspection Date:", "Quantities")
            .RequireCompleteDateToClaimGroup()
            .Build();

        Assert.True(label.RequireCompleteDateToClaimGroup);
    }

    [Fact]
    public void PreviousLines_SetsBothPreviousLinesToFetchAndLeewayBefore()
    {
        // Both fields back this one modifier - a fix that only updated one of them would be a
        // silent half-wiring exactly like the BoundByOtherLabels regression this file exists
        // to catch.
        var label = WrFluentRule.Between("Inspecting Officer", "Licence provisions")
            .PreviousLines(1)
            .Build();

        Assert.Equal(1, label.PreviousLinesToFetch);
        Assert.Equal(1, label.LeewayBefore);
    }

    [Fact]
    public void BoundByOtherLabels_SetsLimitToBoundSameLineWalkByOtherLabelPositions()
    {
        // The exact property whose wiring was silently dropped by an earlier rewrite (the
        // method and backing field survived, Build() just stopped reading it) - restored, and
        // pinned here so a repeat regresses a test instead of silently reappearing.
        var label = WrFluentRule.InOrder("Special conditions", null, "Measurement details")
            .BoundByOtherLabels()
            .Build();

        Assert.True(label.LimitToBoundSameLineWalkByOtherLabelPositions);
    }

    [Fact]
    public void WithoutBoundByOtherLabels_LimitToBoundSameLineWalkByOtherLabelPositionsDefaultsFalse()
    {
        var label = WrFluentRule.InOrder("Source of supply", null, "Quantities").Build();

        Assert.False(label.LimitToBoundSameLineWalkByOtherLabelPositions);
    }

    [Fact]
    public void AllowValueToWrapPastSameLineEndTag_SetsTheFlag()
    {
        var label = WrFluentRule.Between("Meter make", "Reading:")
            .AllowValueToWrapPastSameLineEndTag()
            .Build();

        Assert.True(label.AllowValueToWrapPastSameLineEndTag);
    }

    [Fact]
    public void AllowValueToWrapToNextLine_SetsTheFlag()
    {
        var label = WrFluentRule.After("Met with").AllowValueToWrapToNextLine().Build();

        Assert.True(label.AllowValueToWrapToNextLine);
    }

    [Fact]
    public void DefaultLimitTo_IsSameColumn()
    {
        // The default every rule gets unless it opts into WholeLine() - a silent flip here
        // would change matching behaviour for every rule in the configuration at once.
        var label = WrFluentRule.Between("A", "B").Build();

        Assert.Equal(LimitTo.SameColumn, label.LimitTo);
    }

    [Fact]
    public void WholeLine_SetsLimitToToWholeLine()
    {
        var label = WrFluentRule.Between("Calibration", "Conformance").WholeLine().Build();

        Assert.Equal(LimitTo.WholeLine, label.LimitTo);
    }

    [Fact]
    public void Possibilities_SetsThePossibilitiesList()
    {
        var possibilities = new List<TextToMatch> { new("Yes"), new("No") };

        var label = WrFluentRule.After("Calibration").Possibilities(possibilities).Build();

        Assert.Equal(possibilities.Select(p => p.Text), label.Possibilities!.Select(p => p.Text));
    }

    [Fact]
    public void IgnoreIfContains_SetsIgnoreBlockIfContains()
    {
        var label = WrFluentRule.Between("Calibration", "Verification")
            .IgnoreIfContains("Conformance:", "Maintenance:")
            .Build();

        Assert.Equal(["Conformance:", "Maintenance:"], label.IgnoreBlockIfContains);
    }

    [Fact]
    public void SkipNextLineWhenStartsWith_SetsLimitToExcludeNextLineIfFirstColumnStartsWith()
    {
        var label = WrFluentRule.After("Met with")
            .SkipNextLineWhenStartsWith("Inspecting Officer")
            .Build();

        Assert.Equal(["Inspecting Officer"], label.LimitToExcludeNextLineIfFirstColumnStartsWith);
    }

    [Fact]
    public void EndsAt_ReplacesTextEndWithJustTheOneMarker_NoSentinel()
    {
        // For the After() shape only - there's no [END_OF_BLOCK] sentinel to preserve, since
        // Between()/InOrder()'s own sentinel-preserving AlsoEndsAt is a different method.
        var label = WrFluentRule.After("Calibration").NextLines(1).EndsAt("Conformance").Build();

        var end = Assert.Single(label.TextEnd!);
        Assert.Equal("Conformance", end.Text);
        Assert.True(end.LineMustStartWith);
    }

    [Fact]
    public void AlsoEndsAt_InsertsBeforeTheEndOfBlockSentinel_PreservingItsPosition()
    {
        var label = WrFluentRule.Between("Meter make", "Reading:")
            .AlsoEndsAt("Serial number", "Meter Serial No")
            .Build();

        Assert.Equal(
            ["Reading:", "Serial number", "Meter Serial No", "[END_OF_BLOCK]"],
            label.TextEnd!.Select(t => t.Text));
    }

    [Fact]
    public void AlsoEndsAt_AddedMarkersDoNotRequireLineStart()
    {
        var label = WrFluentRule.Between("Meter make", "Reading:")
            .AlsoEndsAt("Serial number")
            .Build();

        var added = label.TextEnd!.Single(t => t.Text == "Serial number");
        Assert.False(added.LineMustStartWith);
    }

    [Fact]
    public void AlsoEndsAtLineStart_AddedMarkersRequireLineStart()
    {
        var label = WrFluentRule.InOrder("Means of abstraction", null, "Records")
            .AlsoEndsAtLineStart("R ecords", "R e cords")
            .Build();

        Assert.All(
            label.TextEnd!.Where(t => t.Text is "R ecords" or "R e cords"),
            t => Assert.True(t.LineMustStartWith));
    }

    [Fact]
    public void AlsoStartsWith_AppendsToTextStartWithColumnMustStartWith()
    {
        var label = WrFluentRule.Between("Telephone No", "Email")
            .AlsoStartsWith("T e l e p h o n e N o")
            .Build();

        var added = label.TextStart!.Single(t => t.Text == "T e l e p h o n e N o");
        Assert.True(added.ColumnMustStartWith);
    }

    [Fact]
    public void AlsoStartsWith_AlsoAddsToRemove()
    {
        var label = WrFluentRule.Between("Telephone No", "Email")
            .AlsoStartsWith("T e l e p h o n e N o")
            .Build();

        Assert.Contains(label.Remove!, r => r.Text == "T e l e p h o n e N o");
    }

    [Fact]
    public void AlsoStartsWithLoose_AppendsToTextStartWithoutColumnMustStartWith()
    {
        // The whole point of the "loose" variant: no column-boundary requirement, so it can
        // match a label sitting mid-column (see LabelToMatch's own AlsoStartsWithLoose comment).
        var label = WrFluentRule.After("Time").AlsoStartsWithLoose("Time:").Build();

        var added = label.TextStart!.Single(t => t.Text == "Time:");
        Assert.False(added.ColumnMustStartWith);
    }

    [Fact]
    public void Remove_AddsSuppliedItemsToRemove_AlongsideTheAutomaticStartTextEntry()
    {
        var label = WrFluentRule.After("Date:")
            .Remove([new TextToMatch("record:"), new TextToMatch("Conformance:")])
            .Build();

        Assert.Equal(
            ["Date:", "record:", "Conformance:"],
            label.Remove!.Select(r => r.Text));
    }

    [Fact]
    public void ExceptFromRemove_RemovesMatchingEntriesFromRemove()
    {
        // GeneralComments' own case: "Actions"/"Summary" must stay valid TextStart alternates
        // but must not be stripped when they recur as a genuine sub-heading in the captured
        // value - see RuleGeneralComments().
        var label = WrFluentRule.After("General comments")
            .AlsoStartsWith("Actions", "Summary")
            .ExceptFromRemove("Actions", "Summary")
            .Build();

        Assert.DoesNotContain(label.Remove!, r => r.Text is "Actions" or "Summary");
        Assert.Contains(label.TextStart!, t => t.Text == "Actions");
        Assert.Contains(label.TextStart!, t => t.Text == "Summary");
    }

    [Fact]
    public void FromText_SetsLayoutExtractorToLetterBased()
    {
        var label = WrFluentRule.After("Reading:").FromText().Build();

        Assert.Equal(LayoutExtractor.LetterBased, label.LayoutExtractor);
    }

    [Fact]
    public void FromLetterAndTableGrid_SetsExtractorAndGridLookupType()
    {
        var label = WrFluentRule.After("Reading:").FromLetterAndTableGrid().Build();

        Assert.Equal(LayoutExtractor.LetterBasedAndTableBased, label.LayoutExtractor);
        Assert.Equal(LayoutExtractorTableLookupType.Grid, label.LayoutExtractorTableLookupType);
        Assert.Equal(LayoutExtractorTableShape.Unstructured, label.LayoutExtractorTableShape);
    }

    [Fact]
    public void FromLetterAndTableFreeText_SetsExtractorAndFreeTextLookupType()
    {
        var label = WrFluentRule.After("Reading:").FromLetterAndTableFreeText().Build();

        Assert.Equal(LayoutExtractor.LetterBasedAndTableBased, label.LayoutExtractor);
        Assert.Equal(LayoutExtractorTableLookupType.FreeText, label.LayoutExtractorTableLookupType);
    }

    [Fact]
    public void Build_AlwaysSetsFormatToText()
    {
        var label = WrFluentRule.After("Reading:").Build();

        Assert.Equal("Text", label.Format);
    }
}
