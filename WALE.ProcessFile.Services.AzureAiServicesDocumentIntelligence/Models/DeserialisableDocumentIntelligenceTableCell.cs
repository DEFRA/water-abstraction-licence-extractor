using Azure.AI.DocumentIntelligence;

namespace WALE.ProcessFile.Services.AzureAiServicesDocumentIntelligence.Models;

public class DeserialisableDocumentIntelligenceTableCell
{
    public int RowIndex { get; set; }

    public int ColumnIndex { get; set; }

    public string? Content { get; set; }

    public IReadOnlyList<float>? Polygon { get; set; }

    public static DeserialisableDocumentIntelligenceTableCell FromDocumentTableCell(DocumentTableCell cell) => new()
    {
        RowIndex = cell.RowIndex,
        ColumnIndex = cell.ColumnIndex,
        Content = cell.Content,
        Polygon = cell.BoundingRegions.Count > 0 ? cell.BoundingRegions[0].Polygon : null
    };
}
