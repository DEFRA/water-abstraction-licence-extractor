namespace WALE.ProcessFile.Services.Models;

public class DocumentLineWord(string text, double? ocrConfidence, List<double?> coordinates)
{
    public string Text { get; set; } = text;

    public double? OcrConfidence { get; } = ocrConfidence;
    
    public List<double?> Coordinates { get; } = coordinates;
}