using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

/// <summary>
/// The LimitTo.SameColumn/SpecifiedColumn column-matching logic - only reachable for WR51
/// rules (no licence rule sets LimitTo), and only ever exercised end-to-end through real
/// PDFs otherwise. These build DocumentLine/DocumentLineColumn objects by hand so the two
/// steps (find the label's own column on its line, then find the matching column on a
/// following line) can be checked in isolation, in milliseconds.
/// </summary>
public class FindLabelGroupMatchesHelperColumnTests
{
    // DocumentLineWord.Text rejects internal spaces (a "word" is one token) - a column's
    // Text is the space-joined concatenation of its words, so a multi-word phrase here is
    // split into individual single-token words to build a realistic column.
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
            // "Source of supply: ✓ Quantities: ✓" - value sits in the column right after the
            // label, and the walk must stop before sweeping the next field's label in too.
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
            // The label's own column is found via Contains, not an exact/prefix match -
            // separate from whatever TextStart matching decided further upstream.
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
            // This is the real bug found on the WR51 corpus: a column whose text is
            // "record: January 2026" CONTAINS no end marker text here, but more generally,
            // an end marker occurring mid-column (not at its start) must not be treated as
            // the next field starting - only a column that genuinely STARTS a new field
            // should stop the walk. A column that merely mentions the end marker text
            // further in is swept up as part of the value instead.
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
            // The exact shape of the real InspectionDate bug: the label's own line had 3
            // columns, but the date value sits alone on the next line as a single column.
            // Matching by raw column index would have missed this entirely.
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
            // Even though "Close" (index 0) is nearer to the anchor than index 1, a
            // SpecifiedColumn label asked for a specific ordinal column and gets it
            // regardless of position (both stay within the +-100 tolerance of the anchor so
            // this test isolates the index-vs-position selection, not the tolerance check).
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
        public void SameColumn_NullAnchorPosition_TreatsMissingAnchorAsNeverOutOfTolerance()
        {
            // Documents existing behaviour precisely: when anchorLeftPosition is null, the
            // "> anchor + 100" / "< anchor - 100" comparisons involve null arithmetic, and a
            // comparison against null is always false in C# - so the tolerance check can
            // never reject a match. Whichever column is nearest to 0 (the OrderBy fallback)
            // is accepted no matter how far away it actually is.
            var nextLine = LineOf(ColumnAt(5000, "VeryFarButAccepted"));

            var result = FindLabelGroupMatchesHelper.FindNextLineColumnByPosition(
                nextLine, LimitTo.SameColumn, columnIndex: 0, anchorLeftPosition: null);

            Assert.Equal("VeryFarButAccepted", result?.Text);
        }
    }
}
