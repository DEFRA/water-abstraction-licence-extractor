namespace WALE.ProcessFile.Core.Models;

public class DocumentTableCell
{
    public int RowIndex { get; set; }

    public int ColumnIndex { get; set; }

    public string? Content { get; set; }

    public double? Left { get; set; }

    public double? Top { get; set; }
}