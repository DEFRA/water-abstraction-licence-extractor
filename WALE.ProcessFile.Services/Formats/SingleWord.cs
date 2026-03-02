using WALE.ProcessFile.Core.Models;

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

        foreach (var column in line.Columns)
        {
            var text = column.Text.Split(' ')[0];

            var clonedColumn = new DocumentLineColumn(text, column.Words);
            var clonedLine = line.Clone([clonedColumn]);

            labelGroupResult = labelGroupResult.Clone([clonedLine]);
            returnList.Add(labelGroupResult);

            break;
        }

        return returnList;
    }
}