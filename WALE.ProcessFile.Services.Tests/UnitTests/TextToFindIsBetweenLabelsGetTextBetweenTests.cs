using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Methods;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

/// <summary>
/// LabelToMatch.AllowValueToWrapPastSameLineEndTag - only ever exercised end-to-end through
/// real PDFs otherwise (grep confirms zero existing test references it or GetTextBetween
/// itself). Builds DocumentLines by hand so the "stop at the first end-tag hit" vs "keep
/// scanning past it" behaviour can be checked directly, in milliseconds.
/// </summary>
public class TextToFindIsBetweenLabelsGetTextBetweenTests
{
    private static readonly LookupConfiguration Config = new(
        [], [], null!, null!, null!, null!, null!, null!, null!, 0, DateTime.UtcNow);

    private static DocumentLineWord Word(string text) =>
        new(text, null, DocumentLineWordCoordinates.NotKnown(), null);

    private static DocumentLine SingleColumnLine(string text) =>
        new(0, 1,
            [new DocumentLineColumn(text.Split(' ').Select(Word).ToList())],
            0, 0, 0, 0);

    [Fact]
    public void WhenFlagIsOff_StopsAtTheFirstLinesOwnEndTagMatch_IgnoringLaterLines()
    {
        // "first value STOP" - a same-row layout where an unrelated field's marker sits right
        // after this field's real value, on the very first line. Off by default: this is the
        // correct behaviour for every other field, which is why the flag is opt-in.
        var lines = new List<DocumentLine>
        {
            SingleColumnLine("first value STOP"),
            SingleColumnLine("second value"),
            SingleColumnLine("final value STOP")
        };

        var result = TextToFindIsBetweenLabels.GetTextBetween(
            [new TextToMatch("STOP")],
            firstLineTextAfterLabel: null,
            lines,
            startLineNumber: 0,
            lineInput: lines[0],
            labelLineAlreadyIncluded: true,
            doNotTrimLines: false,
            allowValueToWrapPastSameLineEndTag: false,
            Config,
            out var foundEndTag,
            out _);

        Assert.True(foundEndTag);
        Assert.Equal(["first value"], result!.Select(l => l.Text));
    }

    [Fact]
    public void WhenFlagIsOn_KeepsScanningPastTheFirstLinesEndTag_StoppingAtTheRealOneFurtherDown()
    {
        var lines = new List<DocumentLine>
        {
            SingleColumnLine("first value STOP"),
            SingleColumnLine("second value"),
            SingleColumnLine("final value STOP")
        };

        var result = TextToFindIsBetweenLabels.GetTextBetween(
            [new TextToMatch("STOP")],
            firstLineTextAfterLabel: null,
            lines,
            startLineNumber: 0,
            lineInput: lines[0],
            labelLineAlreadyIncluded: true,
            doNotTrimLines: false,
            allowValueToWrapPastSameLineEndTag: true,
            Config,
            out var foundEndTag,
            out _);

        Assert.True(foundEndTag);
        Assert.Equal(["first value", "second value", "final value"], result!.Select(l => l.Text));
    }

    [Fact]
    public void WhenFlagIsOn_ButTheOnlyEndTagHitIsOnTheFirstLine_StillStopsAtEndOfBlock()
    {
        // The flag only lets the walk continue past the FIRST line's own match - it doesn't
        // change what happens afterwards. With no second "STOP" anywhere, and no [END_OF_BLOCK]
        // sentinel in textEnd here, the scan runs out of lines without ever re-finding
        // foundEndTag = true.
        var lines = new List<DocumentLine>
        {
            SingleColumnLine("first value STOP"),
            SingleColumnLine("second value, no marker here")
        };

        var result = TextToFindIsBetweenLabels.GetTextBetween(
            [new TextToMatch("STOP")],
            firstLineTextAfterLabel: null,
            lines,
            startLineNumber: 0,
            lineInput: lines[0],
            labelLineAlreadyIncluded: true,
            doNotTrimLines: false,
            allowValueToWrapPastSameLineEndTag: true,
            Config,
            out var foundEndTag,
            out _);

        Assert.False(foundEndTag);
        Assert.Null(result);
    }

    [Fact]
    public void WhenFlagIsOn_AndEndOfBlockSentinelIsPresent_FallsBackToItWhenNoSecondMatchExists()
    {
        var lines = new List<DocumentLine>
        {
            SingleColumnLine("first value STOP"),
            SingleColumnLine("second value, no marker here")
        };

        var result = TextToFindIsBetweenLabels.GetTextBetween(
            [new TextToMatch("STOP"), new TextToMatch("[END_OF_BLOCK]")],
            firstLineTextAfterLabel: null,
            lines,
            startLineNumber: 0,
            lineInput: lines[0],
            labelLineAlreadyIncluded: true,
            doNotTrimLines: false,
            allowValueToWrapPastSameLineEndTag: true,
            Config,
            out var foundEndTag,
            out _);

        Assert.True(foundEndTag);
        Assert.Equal(["first value", "second value, no marker here"], result!.Select(l => l.Text));
    }
}
