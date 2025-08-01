using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class SingleWord
{
    public const string Constant = "SingleWord";

    public static IReadOnlyList<LabelGroupResult> FindSingleWord(
        IReadOnlyList<DocumentLine> lines,
        LabelGroupResult labelGroupResult)
    {
        var returnList = new List<LabelGroupResult>();

        if (lines.FirstOrDefault() == null)
        {
            return returnList;
        }
        
        var line = lines[0];
        labelGroupResult = labelGroupResult.Clone(
            [line.Clone(line.Text.Split(' ')[0])]);

        returnList.Add(labelGroupResult);
        return returnList;
    }
}