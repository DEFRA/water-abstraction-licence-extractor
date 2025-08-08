using WALE.ProcessFile.Services.Constants;

namespace WALE.ProcessFile.Services.Models;

public class DocumentLine
{
    public DocumentLine(
        int lineNumber,
        int pageNumber,
        List<DocumentLineColumn> columns,
        double bottom,
        double bottomRounded,
        double left)
    {
        LineNumber = lineNumber;
        PageNumber = pageNumber;
        Columns = columns;
        Bottom = bottom;
        BottomRounded = bottomRounded;
        Left = left;
    }
    
    public DocumentLine() : this(
        PositionConstants.UnknownLineNumber,
        PositionConstants.UnknownPageNumber,
        [],
        PositionConstants.UnknownCoordinate,
        PositionConstants.UnknownCoordinate,
        PositionConstants.UnknownCoordinate) { }

    public string Text
    {
        get
        {
            if (Columns.Count == 0)
            {
                return string.Empty;
            }
            
            return Columns[0].Text;
        }
        set
        {
            if (Columns.Count == 0)
            {
                Columns.Add(new DocumentLineColumn());
            }
            
            Columns[0].Text = value;
        }
    }

    public int LineNumber { get; set; }

    public int PageNumber { get; set; }

    public List<DocumentLineColumn> Columns { get; set; } = [];

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

    public double Bottom { get; set; }
    
    public double BottomRounded { get; set; }

    public double Left { get; set; }
    
    public DocumentLine Clone()
    {
        // TODO replace with a source generator        
        
        return new DocumentLine
        {
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
        cloned.Columns[0].Text = text;
        
        return cloned;
    }
}