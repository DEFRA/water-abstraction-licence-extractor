using WALE.ProcessFile.Core.Models;

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
                var matchedPossibilityTextForLine = (string?)null;
                var matchedPossibilityForLine = (TextToMatch?)null;
                
                var newColumns = new List<DocumentLineColumn>();

                foreach (var column in line.Columns)
                {
                    var matchedPossibilityTextForColumn = (string?)null;

                    foreach (var possibility in label.Possibilities!)
                    {
                        if (!column.Text.Contains(possibility.Text, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var possibilityWords = DocumentLineColumn.FilterWordsFromText(
                            column.Words,
                            possibility.Text);
                        
                        var clonedColumn = new DocumentLineColumn(possibilityWords);
                        newColumns.Add(clonedColumn);

                        matchedPossibilityTextForLine = possibility.Text;
                        matchedPossibilityForLine = possibility;
                        
                        matchedPossibilityTextForColumn = possibility.Text;

                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(matchedPossibilityTextForColumn))
                    {
                        continue;
                    }

                    newColumns.Add(column);
                    break;
                }

                if (string.IsNullOrEmpty(matchedPossibilityTextForLine))
                {
                    var previousLine = lines.FirstOrDefault(l => l.LineNumber == line.LineNumber - 1);
                    var linesToLookAt = new List<DocumentLine>();

                    if (previousLine != null)
                    {
                        linesToLookAt.Add(previousLine);
                    }
                    
                    linesToLookAt.Add(line);
                    
                    // Look at this and the last line together for a match
                    var multipleLineText = $"{previousLine?.Text} {line.Text}";

                    foreach (var possibility in label.Possibilities!)
                    {
                        if (!multipleLineText.Contains(possibility.Text, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        
                        var possibilityWords = linesToLookAt
                            .SelectMany(l => l.Columns)
                            .SelectMany(c => c.Words)
                            .ToList();
                        
                        possibilityWords = DocumentLineColumn.FilterWordsFromText(
                            possibilityWords,
                            possibility.Text);
                        
                        var clonedColumn = new DocumentLineColumn(possibilityWords);
                        newColumns.Clear();
                        newColumns.Add(clonedColumn);

                        matchedPossibilityTextForLine = possibility.Text;
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(matchedPossibilityTextForLine))
                    {
                        continue;
                    }
                }

                var clonedLine = line.Clone(newColumns);
                labelGroupResult = labelGroupResult.Clone([clonedLine]);
                labelGroupResult.MatchedLabel!.Possibilities = [matchedPossibilityForLine!];

                return [labelGroupResult];
            }
        }

        return [];
    }
}