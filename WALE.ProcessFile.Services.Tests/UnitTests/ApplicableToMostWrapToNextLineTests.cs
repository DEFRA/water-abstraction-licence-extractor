using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Methods;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

/// <summary>
/// LabelToMatch.AllowValueToWrapToNextLine - only ever exercised end-to-end through real PDFs
/// otherwise (ApplicableToMost.cs has no other direct unit tests at all). The decision this
/// flag drives (BuildResultLinesForConstantFormat) was extracted out of the Text.Constant
/// branch specifically so it could be checked here without needing to build the rest of
/// FunctionAsync's upstream textBeforeAtAndAfterLabel structure by hand.
/// </summary>
public class ApplicableToMostWrapToNextLineTests
{
    private static DocumentLine Line(string text) =>
        new(0, 1,
            [new DocumentLineColumn(text.Split(' ')
                .Select(w => new DocumentLineWord(w, null, DocumentLineWordCoordinates.NotKnown(), null))
                .ToList())],
            0, 0, 0, 0);

    [Fact]
    public void WhenFlagIsOff_ReturnsOnlyTheLabelsOwnLine_EvenWhenNextLinesExist()
    {
        var result = ApplicableToMost.BuildResultLinesForConstantFormat(
            Line("Nathan Atkins"),
            [Line("Continuation line")],
            allowValueToWrapToNextLine: false);

        Assert.Equal(["Nathan Atkins"], result.Select(l => l.Text));
    }

    [Fact]
    public void WhenFlagIsOn_AndNextLinesExist_AppendsThemAfterTheLabelsOwnLine()
    {
        var result = ApplicableToMost.BuildResultLinesForConstantFormat(
            Line("Nathan Atkins"),
            [Line("under Fish Farm RPS")],
            allowValueToWrapToNextLine: true);

        Assert.Equal(["Nathan Atkins", "under Fish Farm RPS"], result.Select(l => l.Text));
    }

    [Fact]
    public void WhenFlagIsOn_ButNextLinesIsNull_ReturnsOnlyTheLabelsOwnLine()
    {
        var result = ApplicableToMost.BuildResultLinesForConstantFormat(
            Line("Nathan Atkins"),
            nextLines: null,
            allowValueToWrapToNextLine: true);

        Assert.Equal(["Nathan Atkins"], result.Select(l => l.Text));
    }

    [Fact]
    public void WhenFlagIsOn_ButNextLinesIsEmpty_ReturnsOnlyTheLabelsOwnLine()
    {
        var result = ApplicableToMost.BuildResultLinesForConstantFormat(
            Line("Nathan Atkins"),
            nextLines: [],
            allowValueToWrapToNextLine: true);

        Assert.Equal(["Nathan Atkins"], result.Select(l => l.Text));
    }

    [Fact]
    public void WhenFlagIsOn_AndMultipleNextLinesExist_AppendsAllOfThemInOrder()
    {
        var result = ApplicableToMost.BuildResultLinesForConstantFormat(
            Line("Simon Mcfarlane"),
            [Line("Ellena Waller-Murray"), Line("Third continuation")],
            allowValueToWrapToNextLine: true);

        Assert.Equal(
            ["Simon Mcfarlane", "Ellena Waller-Murray", "Third continuation"],
            result.Select(l => l.Text));
    }
}
