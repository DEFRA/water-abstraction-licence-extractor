namespace WALE.ProcessFile.Services.Models;

public class DocumentLineWord(string text, double? ocrConfidence, DocumentLineWordCoordinates coordinates)
{
    public string Text { get; set; } = text;

    public double? OcrConfidence { get; } = ocrConfidence;
    
    public DocumentLineWordCoordinates Coordinates { get; } = coordinates;
}