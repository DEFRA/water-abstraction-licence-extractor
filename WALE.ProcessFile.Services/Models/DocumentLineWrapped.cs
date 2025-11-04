using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Services.Models;

public class DocumentLineWrapped
{
    public DocumentLine? Line { get; init; }
    public int Index { get; init; }

    public IReadOnlyList<DocumentLine> PreviousLines(IReadOnlyList<DocumentLineWrapped> lines, LabelToMatch label)
    {
        return GetPreviousLines(lines, Index, label.PreviousLinesToFetch);
    }

    public IReadOnlyList<DocumentLine> NextLines(IReadOnlyList<DocumentLineWrapped> lines, LabelToMatch label)
    {
        return GetNextLines(lines, Index, label.NextLinesToFetch);
    }
    
    private static IReadOnlyList<DocumentLine> GetPreviousLines(IReadOnlyList<DocumentLineWrapped> lines, int startIndex, int numberToTake)
    {
        var newIndex = startIndex - 1;
        var returnList = new List<DocumentLine>();
        var count = 0;

        while (newIndex >= 0 && count++ < numberToTake)
        {
            var line = lines[newIndex];

            returnList.Add(line.Line!);
            newIndex -= 1;
        }

        return returnList;
    }
    
    private static IReadOnlyList<DocumentLine> GetNextLines(IReadOnlyList<DocumentLineWrapped> lines, int index, int n)
    {
        var newIndex = index + 1;
        var returnList = new List<DocumentLine>();
        var count = 0;
        
        while (newIndex < lines.Count && count++ < n)
        {
            var line = lines[newIndex];
            
            returnList.Add(line.Line!);
            newIndex += 1;
        }

        return returnList;
    }
}