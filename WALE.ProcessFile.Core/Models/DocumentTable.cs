namespace WALE.ProcessFile.Core.Models;

public class DocumentTable
{
    public int PageNumber { get; set; }

    public int RowCount { get; set; }

    public int ColumnCount { get; set; }

    public List<DocumentTableCell> Cells { get; set; } = [];
}