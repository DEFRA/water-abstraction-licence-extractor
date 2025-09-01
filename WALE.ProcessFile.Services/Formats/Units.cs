using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class Units
{
    public const string Constant = "Units";

    public static List<LabelGroupResult> GetMatchesToPossibilities(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> lines,
        bool isPrevious,
        LabelGroupResult labelGroupResult)
    {
        if (label.Possibilities == null)
        {
            return [];
        }

        var newLines = isPrevious ? lines.Reverse().ToList() : lines.ToList();
        
        foreach (var line in newLines)
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
                continue;
            }
            
            var clonedLine = line.Clone(newColumns);
            labelGroupResult = labelGroupResult.Clone([clonedLine]);
            labelGroupResult.MatchedLabel!.Possibilities = [matchedPossibilityForLine];

            return [labelGroupResult];
        }

        return [];
    }
}