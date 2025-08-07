using System.Text.Json;
using WALE.ProcessFile.Services.Constants;

namespace WALE.ProcessFile.Services.Models;

public class DocumentLine(
    string text,
    int lineNumber,
    int pageNumber,
    List<DocumentLineWord> words,
    double bottom,
    double bottomRounded,
    double left)
{
    public DocumentLine() : this(
        string.Empty,
        PositionConstants.UnknownLineNumber,
        PositionConstants.UnknownPageNumber,
        [],
        PositionConstants.UnknownCoordinate,
        PositionConstants.UnknownCoordinate,
        PositionConstants.UnknownCoordinate) { }
    
    public DocumentLine(string text) : this(
        text,
        PositionConstants.UnknownLineNumber,
        PositionConstants.UnknownPageNumber,
        [],
        PositionConstants.UnknownCoordinate,
        PositionConstants.UnknownCoordinate,
        PositionConstants.UnknownCoordinate) { }

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

    public double Bottom { get; set; } = bottom;
    
    public double BottomRounded { get; set; } = bottomRounded;

    public double Left { get; set; } = left;
    
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