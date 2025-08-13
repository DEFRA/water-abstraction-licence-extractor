using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class Units
{
    public const string Constant = "Units";

    public static List<LabelGroupResult> GetMatchesToPossibilities(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> lines,
        LabelGroupResult labelGroupResult)
    {
        if (label.Possibilities == null)
        {
            return [];
        }

        foreach (var line in lines)
        {
            var matchedPossibility = (string?)null;
            var newColumns = new List<DocumentLineColumn>();
            
            foreach (var column in line.Columns)
            {
                foreach (var possibility in label.Possibilities!)
                {
                    if (!column.Text.Contains(possibility, StringComparison.InvariantCultureIgnoreCase))
                    {
                        newColumns.Add(column);
                        continue;
                    }

                    var clonedColumn = column.Clone(possibility);
                    newColumns.Add(clonedColumn);

                    matchedPossibility = possibility;
                    break;
                }

                if (!string.IsNullOrWhiteSpace(matchedPossibility))
                {
                    break;
                }
            }

            if (string.IsNullOrEmpty(matchedPossibility))
            {
                continue;
            }
            
            var clonedLine = line.Clone(newColumns);
            labelGroupResult = labelGroupResult.Clone([clonedLine]);
            labelGroupResult.MatchedLabel!.Possibilities = [matchedPossibility];

            return [labelGroupResult];
        }

        return [];
    }
}