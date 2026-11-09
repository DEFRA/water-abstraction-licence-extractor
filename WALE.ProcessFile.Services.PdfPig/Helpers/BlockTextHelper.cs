using WALE.ProcessFile.Services.PdfPig.Models;

namespace WALE.ProcessFile.Services.PdfPig.Helpers;

/// <summary>
/// RecursiveXYCut's blocks already respect a page's columns - on the licences' two-column
/// IMPORTANT NOTES page every one of the 31 sat wholly on one side of the gutter - so reading
/// them in order avoids the interleaving that grouping by vertical position produces.
/// </summary>
public static class BlockTextHelper
{
    private const double BaselineTolerance = 2;

    /// Below this gap the two fragments are one word the cut severed - "of" splitting into
    /// "o" and "f" across a column boundary.
    private const double SameWordGap = 1.5;

    public static IEnumerable<string> Lines(IReadOnlyList<MinimalTextBlock> blocks)
    {
        foreach (var block in blocks)
        {
            foreach (var line in BlockLines(block))
            {
                yield return line;
            }
        }
    }

    private static IEnumerable<string> BlockLines(MinimalTextBlock block)
    {
        var fragments = block.TextLines
            .Select(Fragment)
            .Where(fragment => fragment != null)
            .OrderByDescending(fragment => fragment!.Value.Bottom)
            .ToList();

        var rows = new List<List<(double Bottom, double Left, double Right, string Text)>>();

        foreach (var fragment in fragments)
        {
            // RecursiveXYCut can split one visual line into two textLines and return the right
            // half first, so same-baseline fragments are regrouped and re-ordered by x here.
            if (rows.Count > 0
                && Math.Abs(rows[^1][0].Bottom - fragment!.Value.Bottom) <= BaselineTolerance)
            {
                rows[^1].Add(fragment.Value);
            }
            else
            {
                rows.Add([fragment!.Value]);
            }
        }

        foreach (var row in rows)
        {
            row.Sort((left, right) => left.Left.CompareTo(right.Left));

            var text = row[0].Text;

            for (var index = 1; index < row.Count; index++)
            {
                var separator = row[index].Left - row[index - 1].Right <= SameWordGap ? "" : " ";
                text += separator + row[index].Text;
            }

            text = text.Trim();

            if (text.Length > 0)
            {
                yield return text;
            }
        }
    }

    private static (double Bottom, double Left, double Right, string Text)? Fragment(
        MinimalTextLine textLine)
    {
        var words = textLine.Words
            .OrderBy(word => word.BoundingBox.Left)
            .ToList();

        return words.Count == 0
            ? null
            : (words.Max(word => word.BoundingBox.Bottom),
                words.Min(word => word.BoundingBox.Left),
                words.Max(word => word.BoundingBox.Right),
                string.Join(" ", words.Select(word => word.Text)));
    }
}
