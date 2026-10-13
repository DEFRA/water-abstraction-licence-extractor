using static WALE.ProcessFile.Services.PdfClown.GridCellReconstructor;
using static WALE.ProcessFile.Services.PdfClown.WordToCellAssigner;

namespace WRADI.Services.WrInspectionReport.Tests;

public class WordToCellAssignerTests
{
    private static readonly Cell LeftCell = new(0, 0, 100, 50);
    private static readonly Cell RightCell = new(100, 0, 100, 50);

    [Fact]
    public void WordInsideACell_IsAssignedToIt()
    {
        var word = new WordBox("Source", Left: 10, Right: 40, Top: 40, Bottom: 30);

        var result = AssignWordsToCells([LeftCell, RightCell], [word]);

        Assert.Equal([word], result[LeftCell]);
        Assert.Empty(result[RightCell]);
    }

    [Fact]
    public void WordOutsideEveryCell_IsDropped()
    {
        var word = new WordBox("Stray", Left: 500, Right: 540, Top: 40, Bottom: 30);

        var result = AssignWordsToCells([LeftCell, RightCell], [word]);

        Assert.Empty(result[LeftCell]);
        Assert.Empty(result[RightCell]);
    }

    [Fact]
    public void MultipleWordsInTheSameCell_AreAllCollected()
    {
        var w1 = new WordBox("Source", Left: 5, Right: 30, Top: 40, Bottom: 30);
        var w2 = new WordBox("of", Left: 32, Right: 40, Top: 40, Bottom: 30);
        var w3 = new WordBox("supply:", Left: 42, Right: 80, Top: 40, Bottom: 30);

        var result = AssignWordsToCells([LeftCell, RightCell], [w1, w2, w3]);

        Assert.Equal(3, result[LeftCell].Count);
    }

    [Fact]
    public void CellText_JoinsWordsInReadingOrder_RegardlessOfInputOrder()
    {
        // Two lines: "Other" / "provisions" on the top line, "n/a" on the line below -
        // deliberately shuffled on input to prove the sort, not the input order, decides output.
        WordBox[] words =
        [
            new("n/a", Left: 5, Right: 25, Top: 10, Bottom: 0),
            new("provisions", Left: 40, Right: 90, Top: 40, Bottom: 30),
            new("Other", Left: 5, Right: 35, Top: 40, Bottom: 30)
        ];

        var text = CellText(words);

        Assert.Equal("Other provisions n/a", text);
    }

    [Fact]
    public void CellWithNoWords_ProducesEmptyText()
    {
        Assert.Equal(string.Empty, CellText([]));
    }
}
