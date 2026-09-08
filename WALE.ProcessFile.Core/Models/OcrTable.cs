namespace WALE.ProcessFile.Core.Models;

public class OcrTable
{
    public int PageNumber { get; set; }

    public int RowCount { get; set; }

    public int ColumnCount { get; set; }

    public List<OcrTableCell> Cells { get; set; } = [];
}

public class OcrTableCell
{
    public int RowIndex { get; set; }

    public int ColumnIndex { get; set; }

    public string? Content { get; set; }

    public double? Left { get; set; }

    public double? Top { get; set; }
}
