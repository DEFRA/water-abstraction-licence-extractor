using Azure.AI.DocumentIntelligence;

namespace WALE.ProcessFile.Services.AzureAiServicesDocumentIntelligence.Models;

public class DeserialisableDocumentIntelligenceTable
{
    public int PageNumber { get; set; }

    public int RowCount { get; set; }

    public int ColumnCount { get; set; }

    public List<DeserialisableDocumentIntelligenceTableCell>? Cells { get; set; }

    public static DeserialisableDocumentIntelligenceTable FromDocumentTable(DocumentTable table, int pageNumber) => new()
    {
        PageNumber = pageNumber,
        RowCount = table.RowCount,
        ColumnCount = table.ColumnCount,
        Cells = table.Cells.Select(DeserialisableDocumentIntelligenceTableCell.FromDocumentTableCell).ToList()
    };
}
