using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Constants;

namespace WALE.ProcessFile.Core.Models;

public class DocumentLine(
    int lineNumber,
    int pageNumber,
    List<DocumentLineColumn> columns,
    double top,
    double right,
    double bottom,
    double left)
{
    // ReSharper disable once MemberCanBePrivate.Global
    public DocumentLine() : this(
        PositionConstants.UnknownLineNumber,
        PositionConstants.UnknownPageNumber,
        [],
        PositionConstants.UnknownCoordinate,
        PositionConstants.UnknownCoordinate,        
        PositionConstants.UnknownCoordinate,
        PositionConstants.UnknownCoordinate) { }

    [JsonIgnore]
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
    
    public double Top { get; init; } = top;

    public double Right { get; init; } = right;
    
    public double Bottom { get; init; } = bottom;

    public double Left { get; init; } = left;
    
    public Dictionary<string, object>? AdditionalData { get; set; }
    
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
            Top = Top,
            Right = Right,
            Bottom = Bottom,
            Left = Left
        };
    }
}