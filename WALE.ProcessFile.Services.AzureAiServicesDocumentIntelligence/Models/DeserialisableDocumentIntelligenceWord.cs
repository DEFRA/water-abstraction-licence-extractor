using Azure.AI.DocumentIntelligence;

namespace WALE.ProcessFile.Services.AzureAiServicesDocumentIntelligence.Models;

public class DeserialisableDocumentIntelligenceWord
{
    public string? Content { get; set; }
    
    public float Confidence { get; set; }
    
    public IReadOnlyList<float>? Polygon { get; set; }
    
    public static DeserialisableDocumentIntelligenceWord FromDocumentWord(DocumentWord documentWord)
    {
        
        return new DeserialisableDocumentIntelligenceWord
        {
            Content = documentWord.Content,
            Confidence = documentWord.Confidence,
            Polygon = documentWord.Polygon
        };
    }
}