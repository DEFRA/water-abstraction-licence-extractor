using WALE.ProcessFile.Services.Constants;

namespace WALE.ProcessFile.Services.Models;

public class DocumentLine(
    string text,
    int lineNumber,
    int pageNumber,
    List<DocumentLineColumn> columns,
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

    public List<DocumentLineColumn> Columns { get; set; } = columns;

    public double? OcrConfidence
    {
        get
        {
            var wordsWithConfidence = Columns
                .SelectMany(column => column.Words)
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
        // TODO replace with a source generator        
        
        return new DocumentLine
        {
            Text = Text,
            PageNumber = PageNumber,
            LineNumber = LineNumber,
            Columns = Columns.Select(c => c.Clone()).ToList(),
            Bottom = Bottom,
            BottomRounded = BottomRounded,
            Left = Left
        };
    }
    
    public DocumentLine Clone(string text)
    {
        var cloned = Clone();
        cloned.Text = text;
        
        return cloned;
    }
}