using Azure.AI.DocumentIntelligence;

namespace WALE.ProcessFile.Services.Models.PdfPig.Deserialisable;

public class DeserialisableDocumentIntelligenceLine
{
    public string? Content { get; set; }
    
    public IReadOnlyList<float>? Polygon { get; set; }
    
    public static DeserialisableDocumentIntelligenceLine FromDocumentLine(DocumentLine documentLine)
    {
        return new DeserialisableDocumentIntelligenceLine
        {
            Content = documentLine.Content,
            Polygon = documentLine.Polygon
        };
    }
}