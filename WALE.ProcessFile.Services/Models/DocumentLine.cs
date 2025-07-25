using System.Text.Json;

namespace WALE.ProcessFile.Services.Models;

public class DocumentLine(
    string text,
    int lineNumber,
    int pageNumber,
    List<DocumentLineWord> words,
    double top,
    double topRounded,
    double left,
    double leftRounded)
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

    public double Top { get; set; } = top;
    
    public double TopRounded { get; set; } = topRounded;

    public double Left { get; set; } = left;

    public double LeftRounded { get; set; } = leftRounded;
    
    public DocumentLine Clone()
    {
        return JsonSerializer.Deserialize<DocumentLine>(
            JsonSerializer.Serialize(this))!;
    }
    
    public DocumentLine Clone(string text)
    {
        var cloned = Clone();
        cloned.Text = text;
        
        return cloned;
    }
}