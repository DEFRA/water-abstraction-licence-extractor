using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Services;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

/// <summary>
/// A label group can have several alternates (one per template phrasing). This decides
/// which alternate's match "wins" and stops the search - get it wrong and a later, correct
/// alternate never gets a chance to run, which is exactly the WR51 bug this exists to guard
/// (an alternate matching the label text but capturing nothing used to permanently claim the
/// group). Only reachable end-to-end through the full label-matching pipeline otherwise, so
/// these build LabelGroupResult objects by hand instead.
/// </summary>
public class PdfDataExtractorServiceAlternateSelectionTests
{
    private static LabelGroupResult ResultWithText(params string?[] lineTexts) =>
        new()
        {
            Text = lineTexts.Select(TextLine).ToList()
        };

    private static DocumentLine TextLine(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new DocumentLine(0, 1, [], 0, 0, 0, 0);
        }

        var column = new DocumentLineColumn([
            new DocumentLineWord(text, null, DocumentLineWordCoordinates.NotKnown(), null)
        ]);

        return new DocumentLine(0, 1, [column], 0, 0, 0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReturnsFalse_WhenLabelGroupMatchIsEmpty_RegardlessOfRequireTextToClaimGroup(bool requireText)
    {
        var result = PdfDataExtractorService.ShouldClaimLabelGroup([], requireText);

        Assert.False(result);
    }

    [Fact]
    public void ReturnsTrue_WhenRequireTextToClaimGroupIsFalse_EvenIfTextIsBlank()
    {
        // Every existing rule (licence included) leaves RequireTextToClaimGroup unset, so it
        // must keep claiming on the first match regardless of whether it captured anything -
        // changing this would be a silent behaviour change for every rule in the system.
        var labelGroupMatch = new List<LabelGroupResult> { ResultWithText("") };

        var result = PdfDataExtractorService.ShouldClaimLabelGroup(labelGroupMatch, requireTextToClaimGroup: false);

        Assert.True(result);
    }

    [Fact]
    public void ReturnsFalse_WhenRequireTextToClaimGroupIsTrue_AndEveryResultIsBlank()
    {
        var labelGroupMatch = new List<LabelGroupResult> { ResultWithText(""), ResultWithText("   ") };

        var result = PdfDataExtractorService.ShouldClaimLabelGroup(labelGroupMatch, requireTextToClaimGroup: true);

        Assert.False(result);
    }

    [Fact]
    public void ReturnsTrue_WhenRequireTextToClaimGroupIsTrue_AndAtLeastOneResultHasText()
    {
        var labelGroupMatch = new List<LabelGroupResult> { ResultWithText(""), ResultWithText("Arad") };

        var result = PdfDataExtractorService.ShouldClaimLabelGroup(labelGroupMatch, requireTextToClaimGroup: true);

        Assert.True(result);
    }

    [Fact]
    public void ReturnsTrue_WhenRequireTextToClaimGroupIsTrue_AndOnlyOneOfSeveralLinesHasText()
    {
        // The check is Any() across a single result's lines too, not just across the list of
        // alternates - a multi-line capture where only one line has real text still counts.
        var labelGroupMatch = new List<LabelGroupResult> { ResultWithText("", "", "Arad", "") };

        var result = PdfDataExtractorService.ShouldClaimLabelGroup(labelGroupMatch, requireTextToClaimGroup: true);

        Assert.True(result);
    }

    [Fact]
    public void ReturnsFalse_WhenRequireTextToClaimGroupIsTrue_AndTextIsNull()
    {
        var labelGroupMatch = new List<LabelGroupResult> { new() { Text = null } };

        var result = PdfDataExtractorService.ShouldClaimLabelGroup(labelGroupMatch, requireTextToClaimGroup: true);

        Assert.False(result);
    }

    [Fact]
    public void ReturnsFalse_WhenRequireTextToClaimGroupIsTrue_AndTextIsEmptyList()
    {
        var labelGroupMatch = new List<LabelGroupResult> { new() { Text = [] } };

        var result = PdfDataExtractorService.ShouldClaimLabelGroup(labelGroupMatch, requireTextToClaimGroup: true);

        Assert.False(result);
    }

    [Fact]
    public void TreatsWhitespaceOnlyText_AsBlank_NotAsHavingContent()
    {
        // Two empty columns joined by DocumentLine.Text's space separator produce a line
        // whose Text is literally " " - non-empty, but still whitespace-only. Confirms the
        // check is IsNullOrWhiteSpace, not the narrower IsNullOrEmpty.
        var whitespaceOnlyLine = new DocumentLine(
            0, 1,
            [new DocumentLineColumn([]), new DocumentLineColumn([])],
            0, 0, 0, 0);

        var labelGroupMatch = new List<LabelGroupResult> { new() { Text = [whitespaceOnlyLine] } };

        var result = PdfDataExtractorService.ShouldClaimLabelGroup(labelGroupMatch, requireTextToClaimGroup: true);

        Assert.False(result);
    }
}
