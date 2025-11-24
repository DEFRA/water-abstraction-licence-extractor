namespace WALE.ProcessFile.Core.Models;

public class DocumentLineWord(string text, double? ocrConfidence, DocumentLineWordCoordinates coordinates, string? handwrittenOrTyped)
{
    public string Text { get; set; } = text;

    public double? OcrConfidence { get; } = ocrConfidence;
    
    public DocumentLineWordCoordinates Coordinates { get; } = coordinates;
    
    public string? HandwrittenOrTyped { get; } = handwrittenOrTyped;
}