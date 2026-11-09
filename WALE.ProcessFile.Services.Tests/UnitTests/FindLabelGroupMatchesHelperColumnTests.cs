using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

/// LimitTo.SameColumn and SpecifiedColumn are only reachable for WR51 rules - no licence rule sets
/// LimitTo - and otherwise only run end-to-end through real PDFs.
public class FindLabelGroupMatchesHelperColumnTests
{
    // DocumentLineWord.Text rejects internal spaces, so a phrase has to be split into one word
    // per token to build a realistic column.
    private static DocumentLineColumn Column(params string[] phrases) =>
        new(phrases
            .SelectMany(p => p.Split(' '))
            .Select(w => new DocumentLineWord(w, null, DocumentLineWordCoordinates.NotKnown(), null))
            .ToList());

    private static DocumentLineColumn ColumnAt(double left, params string[] phrases) =>
        new(phrases
            .SelectMany(p => p.Split(' '))
            .Select(w => new DocumentLineWord(w, null, new DocumentLineWordCoordinates(0, 0, 0, left), null))
            .ToList());

    private static DocumentLine LineOf(params DocumentLineColumn[] columns) =>
        new(0, 1, columns.ToList(), 0, 0, 0, 0);

    public class WalkSameLineColumnsTests
    {
        [Fact]
        public void ReturnsOnlyLabelColumn_WhenNoOtherColumnsOnLine()
        {
            var columns = new List<DocumentLineColumn> { Column("Calibration:") };

            var (result, columnIndex) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Calibration", textEnd: null);

            Assert.Equal(["Calibration:"], result.Select(c => c.Text));
            Assert.Equal(0, columnIndex);
        }

        [Fact]
        public void IncludesAdjacentColumns_UntilEndMarkerColumnFound()
        {
            // "Source of supply: ✓ Quantities: ✓" - the walk must stop before the next label.
            var columns = new List<DocumentLineColumn>
            {
                Column("Source of supply:"),
                Column("✓"),
                Column("Quantities:"),
                Column("✓")
            };

            var (result, columnIndex) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Source of supply", [new TextToMatch("Quantities")]);

            Assert.Equal(["Source of supply:", "✓"], result.Select(c => c.Text));
            Assert.Equal(0, columnIndex);
        }

        [Fact]
        public void ReturnsLabelColumnOnly_WhenNextColumnIsImmediatelyTheEndMarker()
        {
            var columns = new List<DocumentLineColumn>
            {
                Column("Inspection Date:"),
                Column("Quantities:")
            };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Inspection Date", [new TextToMatch("Quantities")]);

            Assert.Equal(["Inspection Date:"], result.Select(c => c.Text));
        }

        [Fact]
        public void ReturnsEmpty_WhenLabelTextNotFoundInAnyColumn()
        {
            var columns = new List<DocumentLineColumn> { Column("Other:"), Column("Stuff:") };

            var (result, columnIndex) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Calibration", textEnd: null);

            Assert.Empty(result);
            Assert.Equal(2, columnIndex);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReturnsEmpty_WhenMatchedTextIsNullOrEmpty(string? matchedText)
        {
            var columns = new List<DocumentLineColumn> { Column("Calibration:") };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, matchedText, textEnd: null);

            Assert.Empty(result);
        }

        [Fact]
        public void FindsLabelColumn_ByContainsNotJustExactMatch()
        {
            // The label's own column is found via Contains, not an exact or prefix match.
            var columns = new List<DocumentLineColumn> { Column("Prefix Calibration: Suffix") };

            var (result, columnIndex) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Calibration", textEnd: null);

            Assert.Single(result);
            Assert.Equal(0, columnIndex);
        }

        [Fact]
        public void ReturnsColumnIndexOfLabelColumn_WhenItIsNotFirst()
        {
            var columns = new List<DocumentLineColumn>
            {
                Column("Unrelated:"),
                Column("AlsoUnrelated:"),
                Column("Calibration:")
            };

            var (_, columnIndex) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Calibration", textEnd: null);

            Assert.Equal(2, columnIndex);
        }

        [Fact]
        public void StopsAtWhicheverEndMarkerMatchesFirst_NotJustTheFirstInTheList()
        {
            var columns = new List<DocumentLineColumn>
            {
                Column("Calibration:"),
                Column("x"),
                Column("Meter verification:")
            };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns,
                "Calibration",
                [new TextToMatch("Conformance"), new TextToMatch("Meter verification")]);

            Assert.Equal(["Calibration:", "x"], result.Select(c => c.Text));
        }

        [Fact]
        public void EndMarkerCheckIsCaseInsensitive()
        {
            var columns = new List<DocumentLineColumn> { Column("Calibration:"), Column("CONFORMANCE:") };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Calibration", [new TextToMatch("Conformance")]);

            Assert.Equal(["Calibration:"], result.Select(c => c.Text));
        }

        [Fact]
        public void DoesNotStop_WhenColumnContainsEndMarkerButDoesNotStartWithIt()
        {
            // An end marker mid-column must not stop the walk: only a column that starts a new
            // field does. One merely mentioning the marker is swept up as part of the value.
            var columns = new List<DocumentLineColumn>
            {
                Column("Calibration:"),
                Column("some value mentioning Conformance later")
            };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Calibration", [new TextToMatch("Conformance")]);

            Assert.Equal(["Calibration:", "some value mentioning Conformance later"], result.Select(c => c.Text));
        }

        [Fact]
        public void IgnoresEndMarkersWithEmptyText()
        {
            var columns = new List<DocumentLineColumn> { Column("Calibration:"), Column("x") };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Calibration", [new TextToMatch("")]);

            Assert.Equal(["Calibration:", "x"], result.Select(c => c.Text));
        }

        // Bounds the walk by a sibling field's X-position where no TextEnd marker exists. An
        // earlier rewrite silently dropped the parameter while the caller still built the value,
        // so this is covered to make a repeat a compile error instead of a behaviour change.

        [Fact]
        public void StopsAtNextFieldBoundaryX_EvenWithNoMatchingTextEndMarker()
        {
            var columns = new List<DocumentLineColumn>
            {
                Column("Special conditions:"),
                ColumnAt(400, "some narrative value that never mentions a real end tag")
            };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Special conditions", textEnd: null, nextFieldBoundaryX: 400);

            Assert.Equal(["Special conditions:"], result.Select(c => c.Text));
        }

        [Fact]
        public void DoesNotStopAtNextFieldBoundaryX_WhenColumnIsBeforeIt()
        {
            var columns = new List<DocumentLineColumn>
            {
                Column("Special conditions:"),
                ColumnAt(200, "genuine value")
            };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Special conditions", textEnd: null, nextFieldBoundaryX: 400);

            Assert.Equal(["Special conditions:", "genuine value"], result.Select(c => c.Text));
        }

        [Fact]
        public void StopsAtNextFieldBoundaryX_WhenColumnLeftIsExactlyOnTheBoundary()
        {
            // >=, not >: a column starting exactly at the next field's position belongs to it.
            var columns = new List<DocumentLineColumn>
            {
                Column("Special conditions:"),
                ColumnAt(400, "value")
            };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Special conditions", textEnd: null, nextFieldBoundaryX: 400);

            Assert.Equal(["Special conditions:"], result.Select(c => c.Text));
        }

        [Fact]
        public void NeverStopsOnPosition_WhenNextFieldBoundaryXIsNull()
        {
            // A rule that does not set the flag must behave as if the parameter did not exist.
            var columns = new List<DocumentLineColumn>
            {
                Column("Special conditions:"),
                ColumnAt(999999, "far away value")
            };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Special conditions", textEnd: null, nextFieldBoundaryX: null);

            Assert.Equal(["Special conditions:", "far away value"], result.Select(c => c.Text));
        }

        [Fact]
        public void DoesNotStopAtBoundary_WhenCandidateColumnHasNoWords()
        {
            // No words means no position to compare, so the column is included, not a boundary.
            var columns = new List<DocumentLineColumn>
            {
                Column("Special conditions:"),
                new DocumentLineColumn([])
            };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns, "Special conditions", textEnd: null, nextFieldBoundaryX: 0);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void TextEndMarkerStillStops_EvenWhenNextFieldBoundaryXIsFurtherAway()
        {
            // The two stop conditions are independent: a TextEnd match stops the walk early.
            var columns = new List<DocumentLineColumn>
            {
                Column("Special conditions:"),
                ColumnAt(50, "Measurement details")
            };

            var (result, _) = FindLabelGroupMatchesHelper.WalkSameLineColumns(
                columns,
                "Special conditions",
                [new TextToMatch("Measurement details")],
                nextFieldBoundaryX: 999);

            Assert.Equal(["Special conditions:"], result.Select(c => c.Text));
        }
    }

    public class ShouldExcludeNextLineTests
    {
        [Fact]
        public void WhenExcludeListIsNullOrEmpty_ThenNeverExcludes()
        {
            var nextLine = LineOf(Column("Maintenance:"), Column("Y:"));

            Assert.False(FindLabelGroupMatchesHelper.ShouldExcludeNextLine(nextLine, null));
            Assert.False(FindLabelGroupMatchesHelper.ShouldExcludeNextLine(nextLine, []));
        }

        [Fact]
        public void WhenFirstColumnStartsWithAnExcludedText_ThenExcludes()
        {
            // "Maintenance: Y: N: Frequency: By whom:" sits immediately below the shared
            // Calibration/Conformance/FlowVerification/MeterVerification label row, so its "Y:"
            // and "N:" must not be taken as one of those four fields' values.
            var nextLine = LineOf(Column("Maintenance:"), Column("Y:"), Column("N:"));

            var result = FindLabelGroupMatchesHelper.ShouldExcludeNextLine(nextLine, ["Maintenance"]);

            Assert.True(result);
        }

        [Fact]
        public void WhenFirstColumnDoesNotStartWithAnyExcludedText_ThenDoesNotExclude()
        {
            var nextLine = LineOf(Column("✓"));

            var result = FindLabelGroupMatchesHelper.ShouldExcludeNextLine(nextLine, ["Maintenance"]);

            Assert.False(result);
        }

        [Fact]
        public void WhenExcludedTextAppearsButNotInTheFirstColumn_ThenDoesNotExclude()
        {
            // Only the row's leading column counts, or a real value row mentioning the name is
            // rejected.
            var nextLine = LineOf(Column("✓"), Column("Maintenance:"));

            var result = FindLabelGroupMatchesHelper.ShouldExcludeNextLine(nextLine, ["Maintenance"]);

            Assert.False(result);
        }

        [Fact]
        public void WhenLineHasNoColumns_ThenDoesNotExclude()
        {
            var nextLine = LineOf();

            var result = FindLabelGroupMatchesHelper.ShouldExcludeNextLine(nextLine, ["Maintenance"]);

            Assert.False(result);
        }

        [Fact]
        public void MatchIsCaseInsensitive()
        {
            var nextLine = LineOf(Column("maintenance:"));

            var result = FindLabelGroupMatchesHelper.ShouldExcludeNextLine(nextLine, ["Maintenance"]);

            Assert.True(result);
        }
    }

    public class FindNextLineColumnByPositionTests
    {
        [Fact]
        public void SameColumn_PicksClosestColumnByXPosition()
        {
            var nextLine = LineOf(ColumnAt(0, "Far"), ColumnAt(340, "Close"), ColumnAt(900, "Farthest"));

            var result = FindLabelGroupMatchesHelper.FindNextLineColumnByPosition(
                nextLine, LimitTo.SameColumn, columnIndex: 1, anchorLeftPosition: 338);

            Assert.Equal("Close", result?.Text);
        }

        [Fact]
        public void SameColumn_FindsSingleColumnLine_EvenThoughLabelLineHadMoreColumns()
        {
            // The InspectionDate shape: three columns on the label's line, the value alone on the
            // next. Matching by raw column index misses it.
            var nextLine = LineOf(ColumnAt(338, "05/06/2026"));

            var result = FindLabelGroupMatchesHelper.FindNextLineColumnByPosition(
                nextLine, LimitTo.SameColumn, columnIndex: 1, anchorLeftPosition: 338);

            Assert.Equal("05/06/2026", result?.Text);
        }

        [Fact]
        public void SameColumn_RejectsMatch_WhenClosestColumnIsTooFarAway()
        {
            var nextLine = LineOf(ColumnAt(900, "Unrelated"));

            var result = FindLabelGroupMatchesHelper.FindNextLineColumnByPosition(
                nextLine, LimitTo.SameColumn, columnIndex: 0, anchorLeftPosition: 0);

            Assert.Null(result);
        }

        [Fact]
        public void SameColumn_AcceptsMatch_ExactlyAtTheToleranceBoundary()
        {
            var nextLine = LineOf(ColumnAt(100, "Boundary"));

            var result = FindLabelGroupMatchesHelper.FindNextLineColumnByPosition(
                nextLine, LimitTo.SameColumn, columnIndex: 0, anchorLeftPosition: 0);

            Assert.Equal("Boundary", result?.Text);
        }

        [Fact]
        public void SameColumn_RejectsMatch_JustOutsideTheToleranceBoundary()
        {
            var nextLine = LineOf(ColumnAt(100.1, "JustOutside"));

            var result = FindLabelGroupMatchesHelper.FindNextLineColumnByPosition(
                nextLine, LimitTo.SameColumn, columnIndex: 0, anchorLeftPosition: 0);

            Assert.Null(result);
        }

        [Fact]
        public void SameColumn_SkipsColumnsWithNoWords()
        {
            var emptyColumn = new DocumentLineColumn([]);
            var nextLine = LineOf(emptyColumn, ColumnAt(0, "Real"));

            var result = FindLabelGroupMatchesHelper.FindNextLineColumnByPosition(
                nextLine, LimitTo.SameColumn, columnIndex: 0, anchorLeftPosition: 0);

            Assert.Equal("Real", result?.Text);
        }

        [Fact]
        public void SameColumn_ReturnsNull_WhenLineHasNoColumns()
        {
            var nextLine = LineOf();

            var result = FindLabelGroupMatchesHelper.FindNextLineColumnByPosition(
                nextLine, LimitTo.SameColumn, columnIndex: 0, anchorLeftPosition: 0);

            Assert.Null(result);
        }

        [Fact]
        public void SpecifiedColumn_UsesFixedIndex_IgnoringPosition()
        {
            // SpecifiedColumn gets the ordinal it asked for even though index 0 is nearer the
            // anchor. Both are inside the tolerance, so only index-vs-position is under test.
            var nextLine = LineOf(ColumnAt(338, "Close"), ColumnAt(380, "RequestedIndexNotClosest"));

            var result = FindLabelGroupMatchesHelper.FindNextLineColumnByPosition(
                nextLine, LimitTo.SpecifiedColumn, columnIndex: 1, anchorLeftPosition: 338);

            Assert.Equal("RequestedIndexNotClosest", result?.Text);
        }

        [Fact]
        public void SpecifiedColumn_ReturnsNull_WhenIndexOutOfRange()
        {
            var nextLine = LineOf(ColumnAt(0, "OnlyColumn"));

            var result = FindLabelGroupMatchesHelper.FindNextLineColumnByPosition(
                nextLine, LimitTo.SpecifiedColumn, columnIndex: 5, anchorLeftPosition: 0);

            Assert.Null(result);
        }

        [Fact]
        public void SameColumn_NullAnchorPosition_RejectsRatherThanAcceptingAnyDistance()
        {
            // An unknown anchor returns null rather than accepting the nearest column. Nullable
            // arithmetic ("> anchor + 100" with a null anchor) is always false in C#, so the
            // earlier comparison could never reject anything.
            var nextLine = LineOf(ColumnAt(5000, "VeryFarAndNowCorrectlyRejected"));

            var result = FindLabelGroupMatchesHelper.FindNextLineColumnByPosition(
                nextLine, LimitTo.SameColumn, columnIndex: 0, anchorLeftPosition: null);

            Assert.Null(result);
        }
    }
}
