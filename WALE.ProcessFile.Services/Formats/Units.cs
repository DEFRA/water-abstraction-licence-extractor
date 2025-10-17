using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class Units
{
    public const string Constant = "Units";

    public static List<LabelGroupResult> GetMatchesToPossibilities(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> lines,
        bool lineNumbersAreDescending,
        LabelGroupResult labelGroupResult)
    {
        if (label.Possibilities == null)
        {
            return [];
        }

        var lineGroups = new List<List<DocumentLine>>();
        var initialLine = lines.FirstOrDefault(l => l.LineNumber == labelGroupResult.LineNumber);

        if (initialLine != null)
        {
            lineGroups.Add([initialLine!]);
        }

        if (lines.Count > 1 || lineGroups.Count == 0)
        {
            lineGroups.Add(lineNumbersAreDescending
                ? lines.OrderByDescending(x => x.LineNumber).ToList()
                : lines.OrderBy(x => x.LineNumber).ToList());
        }

        foreach (var lineGroup in lineGroups)
        {
            foreach (var line in lineGroup)
            {
                var matchedPossibilityForLine = (string?)null;
                var newColumns = new List<DocumentLineColumn>();

                foreach (var column in line.Columns)
                {
                    var matchedPossibilityForColumn = (string?)null;

                    foreach (var possibility in label.Possibilities!)
                    {
                        if (!column.Text.Contains(possibility, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }

                        var clonedColumn = new DocumentLineColumn(possibility);
                        newColumns.Add(clonedColumn);

                        matchedPossibilityForLine = possibility;
                        matchedPossibilityForColumn = possibility;

                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(matchedPossibilityForColumn))
                    {
                        continue;
                    }

                    newColumns.Add(column);
                    break;
                }

                if (string.IsNullOrEmpty(matchedPossibilityForLine))
                {
                    var previousLine = lines.FirstOrDefault(l => l.LineNumber == line.LineNumber - 1);

                    // Look at this and the last line together for a match
                    var multipleLineText = $"{previousLine?.Text} {line.Text}";

                    foreach (var possibility in label.Possibilities!)
                    {
                        if (!multipleLineText.Contains(possibility, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }

                        var clonedColumn = new DocumentLineColumn(possibility);
                        newColumns.Clear();
                        newColumns.Add(clonedColumn);

                        matchedPossibilityForLine = possibility;
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(matchedPossibilityForLine))
                    {
                        continue;
                    }
                }

                var clonedLine = line.Clone(newColumns);
                labelGroupResult = labelGroupResult.Clone([clonedLine]);
                labelGroupResult.MatchedLabel!.Possibilities = [matchedPossibilityForLine];

                return [labelGroupResult];
            }
        }

        return [];
    }
}