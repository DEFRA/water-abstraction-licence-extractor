using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Enums;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Direct unit tests for WrInspectionReportSchemaConverter.ToForm's template classification
/// (Metadata.Template), isolated from PDF extraction - same pattern as
/// WrInspectionReportSchemaConverterTests, a hand-built MatchesResult with just the marker
/// label groups populated is enough to exercise ClassifyTemplate's priority order without a
/// real or dummy PDF fixture. Added because none of the 11 WR51 dummy fixtures assert
/// Metadata.Template at all, and the priority order/blank-comments nuance here were both hard
/// to get right the first time (see wr51_column_walk_bug memory and this session's history) -
/// exactly the kind of logic a silent regression could slip through undetected.
/// </summary>
public class WrTemplateClassificationTests
{
    private static MatchesResult BuildMatchesResult(string? documentHeader, params string[] presentMarkerNames)
    {
        var matches = new List<LabelGroupResult>();

        if (documentHeader != null)
        {
            matches.Add(BuildMatch("DocumentHeader", documentHeader));
        }

        foreach (var markerName in presentMarkerNames)
        {
            matches.Add(BuildMatch(markerName, markerName)); // text content doesn't matter, only presence
        }

        return new MatchesResult { Matches = matches };
    }

    private static LabelGroupResult BuildMatch(string labelGroupName, string text)
    {
        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords(text, null))],
            0,
            0,
            0,
            0);

        return new LabelGroupResult
        {
            Text = [documentLine],
            LabelGroupName = labelGroupName,
            MatchedLabelName = labelGroupName
        };
    }

    [Fact]
    public void WhenOnlyDocumentHeaderAndBaselineCommentsPresent_ThenT1()
    {
        var matchesResult = BuildMatchesResult("51", "TemplateMarkerBaselineComments");

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.T1, form.Metadata.Template);
    }

    [Fact]
    public void WhenCommentsSectionHasNeitherBaselineNorAlternateHeading_ThenNonStandardNarrative()
    {
        // A deliberate, evidenced tradeoff, not an oversight - two candidate rules were
        // measured against the golden set: treating "no heading at all" as T1 (alternate-only
        // exclusion) missed 6 genuinely non-standard documents (headingless narratives,
        // multi-section reports) that this rule catches; treating it as NonStandardNarrative
        // (this rule, chosen) costs 2 false positives on documents that are genuinely T1 but
        // happen to have nothing written in that section. The 2-false-positive version won.
        // See WrInspectionReportSchemaConverter.ClassifyTemplate's own comment for the exact
        // documents and counts this was measured against.
        var matchesResult = BuildMatchesResult("51");

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.NonStandardNarrative, form.Metadata.Template);
    }

    [Fact]
    public void WhenDocumentHeaderMissing_ThenUnknown()
    {
        var matchesResult = BuildMatchesResult(null);

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.Unknown, form.Metadata.Template);
    }

    [Fact]
    public void WhenDocumentHeaderMissingButAT4MarkerMatched_ThenT4NotUnknown()
    {
        // The positive template markers take priority over the missing-header check - a
        // document can be confidently T4 even if DocumentHeader's own alternate happened not
        // to match.
        var matchesResult = BuildMatchesResult(null, "TemplateMarkerT4");

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.T4, form.Metadata.Template);
    }

    [Fact]
    public void WhenImpoundingMarkerPresent_ThenImpoundingRegardlessOfOtherMarkers()
    {
        // Impounding is checked first - a document that also happens to trip the T4 marker
        // (e.g. shares some label wording) must still classify as Impounding, since that's a
        // different licence type entirely, not a template variant of an abstraction licence.
        var matchesResult = BuildMatchesResult("51", "TemplateMarkerImpounding", "TemplateMarkerT4");

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.Impounding, form.Metadata.Template);
    }

    [Fact]
    public void WhenT4AndT6MarkersBothPresent_ThenT4Wins()
    {
        var matchesResult = BuildMatchesResult("51", "TemplateMarkerT4", "TemplateMarkerT6");

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.T4, form.Metadata.Template);
    }

    [Fact]
    public void WhenT6AndT7MarkersBothPresent_ThenT6Wins()
    {
        var matchesResult = BuildMatchesResult("51", "TemplateMarkerT6", "TemplateMarkerT7");

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.T6, form.Metadata.Template);
    }

    [Fact]
    public void WhenOnlyT7MarkerPresent_ThenT7()
    {
        var matchesResult = BuildMatchesResult("51", "TemplateMarkerT7");

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.T7, form.Metadata.Template);
    }

    [Fact]
    public void WhenAlternateCommentsHeadingPresent_ThenNonStandardNarrative()
    {
        var matchesResult = BuildMatchesResult("51", "TemplateMarkerAlternateComments");

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.NonStandardNarrative, form.Metadata.Template);
    }

    [Fact]
    public void WhenBothBaselineAndAlternateCommentsHeadingsPresent_ThenAlternateWins()
    {
        // A document with the baseline heading text somewhere AND a recognised alternate
        // heading (e.g. "Notes and Actions") elsewhere is not a clean T1 document - the
        // alternate heading's presence is what actually drives this field's real shape.
        var matchesResult = BuildMatchesResult("51", "TemplateMarkerBaselineComments", "TemplateMarkerAlternateComments");

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.NonStandardNarrative, form.Metadata.Template);
    }
}
