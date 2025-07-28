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
        var returnList = new List<LabelGroupResult>();

        if (label.Possibilities == null)
        {
            return returnList;
        }

        foreach (var previousLine in lines)
        {
            foreach (var possibility in label.Possibilities!)
            {
                if (!previousLine.Text.Contains(possibility, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                labelGroupResult.Text = [previousLine.Clone(possibility)];
                labelGroupResult.MatchedLabel!.Possibilities = [possibility];

                returnList.Add(labelGroupResult);
            }
        }

        return returnList;
    }
}