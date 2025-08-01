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
            foreach (var possibility in label.Possibilities!)
            {
                if (!line.Text.Contains(possibility, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                labelGroupResult = labelGroupResult.Clone([line.Clone(possibility)]);
                labelGroupResult.MatchedLabel!.Possibilities = [possibility];

                return [labelGroupResult];
            }
        }

        return [];
    }
}