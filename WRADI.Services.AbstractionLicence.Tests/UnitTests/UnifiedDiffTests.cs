using WRADI.Services.Output.AbstractionLicence;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class UnifiedDiffTests
{
    private static string Render(IEnumerable<LicenceRunDiffLine> lines) =>
        string.Join(
            "\n",
            lines.Select(line => line.Kind switch
            {
                LicenceRunDiffLineKind.Removed => $"-{line.Text}",
                LicenceRunDiffLineKind.Added => $"+{line.Text}",
                LicenceRunDiffLineKind.Skipped => "@@",
                _ => $" {line.Text}"
            }));

    /// Every unchanged run is reported as a count, this one included - the service only asks
    /// for a diff once it already knows the two differ.
    [Fact]
    public void WhenNothingChanged_ThenTheWholeDocumentCollapsesToOneCount()
    {
        string[] lines = ["a", "b", "c"];

        var only = Assert.Single(UnifiedDiff.Build(lines, lines));

        Assert.Equal(LicenceRunDiffLineKind.Skipped, only.Kind);
        Assert.Equal("3 unchanged lines", only.Text);
    }

    [Fact]
    public void WhenALineChanged_ThenItIsRemovedAndAddedAroundTheSameContext()
    {
        var diff = UnifiedDiff.Build(["a", "b", "c"], ["a", "B", "c"]);

        Assert.Equal(" a\n-b\n+B\n c", Render(diff));
    }

    [Fact]
    public void WhenALineWasInsertedOnly_ThenNothingIsMarkedRemoved()
    {
        var diff = UnifiedDiff.Build(["a", "c"], ["a", "b", "c"]);

        Assert.Equal(" a\n+b\n c", Render(diff));
    }

    [Fact]
    public void WhenALineWasDeletedOnly_ThenNothingIsMarkedAdded()
    {
        var diff = UnifiedDiff.Build(["a", "b", "c"], ["a", "c"]);

        Assert.Equal(" a\n-b\n c", Render(diff));
    }

    /// Without it the diff of a 250-line document is the whole document.
    [Fact]
    public void WhenAStretchIsUnchanged_ThenOnlyTheContextSurvivesAndTheRestIsCounted()
    {
        var older = Enumerable.Range(0, 30).Select(line => $"line {line}").ToArray();
        var newer = older.ToArray();

        newer[0] = "changed";

        var diff = UnifiedDiff.Build(older, newer);

        Assert.Equal(
            ["-line 0", "+changed", " line 1", " line 2", " line 3", "@@"],
            Render(diff).Split('\n'));

        Assert.Equal(
            "26 unchanged lines",
            diff.Single(line => line.Kind == LicenceRunDiffLineKind.Skipped).Text);
    }

    [Fact]
    public void WhenTwoChangesAreCloseTogether_ThenTheirContextJoinsRatherThanRepeating()
    {
        var older = Enumerable.Range(0, 12).Select(line => $"line {line}").ToArray();
        var newer = older.ToArray();

        newer[4] = "first";
        newer[6] = "second";

        var diff = UnifiedDiff.Build(older, newer);

        Assert.DoesNotContain(
            diff.SkipWhile(line => line.Kind != LicenceRunDiffLineKind.Removed)
                .TakeWhile(line => line.Kind != LicenceRunDiffLineKind.Skipped),
            line => line.Kind == LicenceRunDiffLineKind.Skipped);

        Assert.Equal(2, diff.Count(line => line.Kind == LicenceRunDiffLineKind.Removed));
    }

    /// The numbers are what let a reader line the two sides up, so an off-by-one is the bug
    /// most likely to go unnoticed.
    [Fact]
    public void WhenLinesAreNumbered_ThenEachSideCountsOnlyItsOwn()
    {
        var diff = UnifiedDiff.Build(["a", "b", "c"], ["a", "B", "c"], contextLines: -1);

        Assert.Equal([1, 2, null, 3], diff.Select(line => line.OlderLineNumber));
        Assert.Equal([1, null, 2, 3], diff.Select(line => line.NewerLineNumber));
    }

    [Fact]
    public void WhenOneSideIsEmpty_ThenEveryLineOfTheOtherShows()
    {
        Assert.Equal("+a\n+b", Render(UnifiedDiff.Build([], ["a", "b"])));
        Assert.Equal("-a\n-b", Render(UnifiedDiff.Build(["a", "b"], [])));
    }
}
