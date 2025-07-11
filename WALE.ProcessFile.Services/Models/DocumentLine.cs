namespace WALE.ProcessFile.Services.Models;

public class DocumentLine(
    string text,
    int lineNumber,
    int pageNumber,
    List<DocumentLineWord> words)
{
    public string Text { get; set; } = text;

    public int LineNumber { get; set; } = lineNumber;

    public int PageNumber { get; set; } = pageNumber;

    public List<DocumentLineWord> Words { get; set; } = words;

    public double? OcrConfidence
    {
        get
        {
            var wordsWithConfidence = Words
                .Where(word => word.OcrConfidence != null)
                .ToList();

            if (wordsWithConfidence.Count == 0)
            {
                return null;
            }

            var total = wordsWithConfidence.Sum(word => word.OcrConfidence!.Value);
            return total / wordsWithConfidence.Count;
        }
    }
}