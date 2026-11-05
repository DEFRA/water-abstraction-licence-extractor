using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Enums;

namespace WRADI.Services.WrInspectionReport.Tests;

/// A hand-built MatchesResult with only the marker groups populated exercises ClassifyTemplate's
/// priority order; none of the 11 dummy PDF fixtures assert Metadata.Template at all.
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
        // The 2 false positives are the chosen cost; see ClassifyTemplate for the measurement.
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
        // A document can be confidently T4 even where DocumentHeader's alternate did not match.
        var matchesResult = BuildMatchesResult(null, "TemplateMarkerT4");

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.T4, form.Metadata.Template);
    }

    [Fact]
    public void WhenImpoundingMarkerPresent_ThenImpoundingRegardlessOfOtherMarkers()
    {
        // Impounding wins over a tripped T4 marker: it is a different licence type, not a variant.
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
        // The alternate heading's presence drives the field's shape, so this is not a clean T1.
        var matchesResult = BuildMatchesResult("51", "TemplateMarkerBaselineComments", "TemplateMarkerAlternateComments");

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, null);

        Assert.Equal(WrTemplateType.NonStandardNarrative, form.Metadata.Template);
    }
}