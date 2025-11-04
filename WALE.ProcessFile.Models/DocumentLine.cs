using WALE.ProcessFile.Models.Constants;

namespace WALE.ProcessFile.Models;

public class DocumentLine(
    int lineNumber,
    int pageNumber,
    List<DocumentLineColumn> columns,
    double bottom,
    double bottomRounded,
    double left)
{
    // ReSharper disable once MemberCanBePrivate.Global
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
            return Columns.Count == 0 ?
                string.Empty
                : string.Join(' ', Columns.Select(column => column.Text));
        }
    }

    public int LineNumber { get; set; } = lineNumber;

    public int PageNumber { get; init; } = pageNumber;

    public List<DocumentLineColumn> Columns { get; set; } = columns;

    public double? OcrConfidence
    {
        get
        {
            var columnsWithConfidence = Columns
                .Where(column => column.OcrConfidence != null)
                .Select(column => column.OcrConfidence)
                .ToList();

            if (columnsWithConfidence.Count == 0)
            {
                return null;
            }

            var totalConfidence = columnsWithConfidence.Sum(confidence => confidence!.Value);
            return totalConfidence / columnsWithConfidence.Count;
        }
    }

    public double Bottom { get; init; } = bottom;

    public double BottomRounded { get; init; } = bottomRounded;

    public double Left { get; init; } = left;
    
    public DocumentLine Clone(List<DocumentLineColumn> columns)
    {
        var cloned = Clone();
        cloned.Columns = columns;
        
        return cloned;
    }
    
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
}