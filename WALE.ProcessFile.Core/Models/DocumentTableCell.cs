namespace WALE.ProcessFile.Core.Models;

public class DocumentTableCell
{
    public int RowIndex { get; set; }

    public int ColumnIndex { get; set; }

    public string? Content { get; set; }

    public double? Left { get; set; }

    public double? Top { get; set; }

    /// With Left and Top, the cell's own rectangle. Null where the extractor does not report one,
    /// in which case a value matched from this cell has no region to outline.
    public double? Right { get; set; }

    public double? Bottom { get; set; }

    public bool HasRectangle => Left.HasValue && Top.HasValue && Right.HasValue && Bottom.HasValue;

    /// The rectangle as word coordinates, so a value matched from a cell carries a region the way
    /// a value matched from positioned words does.
    public DocumentLineWordCoordinates? ToCoordinates() =>
        HasRectangle
            ? new DocumentLineWordCoordinates(Top!.Value, Right!.Value, Bottom!.Value, Left!.Value)
            : null;
}