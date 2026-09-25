using static WALE.ProcessFile.Services.PdfClown.GridCellReconstructor;

namespace WALE.ProcessFile.Services.PdfClown;

/// <summary>
/// Pure geometry: assign each word's bounding box to whichever reconstructed grid cell
/// contains its center point. Independent of PdfPig/PdfClown types (a WordBox is just 4
/// numbers) so it can be tested with synthetic data, same as GridCellReconstructor.
///
/// Center-point containment, not overlap area: a word's box can legitimately straddle a cell
/// boundary by a point or two (font metrics vs. drawn border position never align exactly),
/// but its center is a stable, unambiguous single point to test.
/// </summary>
public static class WordToCellAssigner
{
    public readonly record struct WordBox(string Text, double Left, double Right, double Top, double Bottom);

    public static Dictionary<Cell, List<WordBox>> AssignWordsToCells(
        IReadOnlyList<Cell> cells,
        IEnumerable<WordBox> words)
    {
        var result = cells.ToDictionary(c => c, _ => new List<WordBox>());

        foreach (var word in words)
        {
            var cell = FindContainingCell(cells, word);

            if (cell is { } found)
            {
                result[found].Add(word);
            }
        }

        return result;
    }

    private static Cell? FindContainingCell(IReadOnlyList<Cell> cells, WordBox word)
    {
        var centerX = (word.Left + word.Right) / 2;
        var centerY = (word.Top + word.Bottom) / 2;

        foreach (var cell in cells)
        {
            if (centerX >= cell.X && centerX <= cell.X + cell.Width
                && centerY >= cell.Y && centerY <= cell.Y + cell.Height)
            {
                return cell;
            }
        }

        return null;
    }

    // Reading order within a cell: bucket words into visual lines first (by Y-center, within
    // tolerance), top line first (largest Y, PdfPig's Y-up convention), then left to right
    // within each line - a pure per-word Y sort breaks as soon as two words on the same line
    // have slightly different baselines (sub-point font/kerning noise), which real PdfPig
    // output does have.
    public static string CellText(IEnumerable<WordBox> words, double lineTolerance = 3.0)
    {
        var remaining = words
            .Select(w => (Word: w, CenterY: (w.Top + w.Bottom) / 2))
            .OrderByDescending(w => w.CenterY)
            .ToList();

        var lines = new List<List<(WordBox Word, double CenterY)>>();

        foreach (var item in remaining)
        {
            var line = lines.FirstOrDefault(l => Math.Abs(l[0].CenterY - item.CenterY) <= lineTolerance);

            if (line == null)
            {
                line = [];
                lines.Add(line);
            }

            line.Add(item);
        }

        return string.Join(' ', lines
            .OrderByDescending(l => l.Average(w => w.CenterY))
            .SelectMany(l => l.OrderBy(w => w.Word.Left))
            .Select(w => w.Word.Text));
    }
}
