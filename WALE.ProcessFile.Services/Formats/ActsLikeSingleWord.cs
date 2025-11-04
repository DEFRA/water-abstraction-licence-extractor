using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class ActsLikeSingleWord
{
    public const string Constant = "ActsLikeSingleWord";

    public static IReadOnlyList<LabelGroupResult> FindSingleWord(
        IReadOnlyList<DocumentLine> lines,
        LabelGroupResult labelGroupResult)
    {
        return SingleWord.FindSingleWord(lines, labelGroupResult);
    }
}