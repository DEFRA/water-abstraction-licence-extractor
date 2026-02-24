using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models;

public class DocumentLineWord(
    string text,
    double? ocrConfidence,
    DocumentLineWordCoordinates coordinates,
    string? handwrittenOrTyped)
{
    [JsonConstructor]
    public DocumentLineWord() :
        this(string.Empty, null, DocumentLineWordCoordinates.NotKnown(), null) { }
    
    public string Text { get; set; } = text;

    public double? OcrConfidence { get; set; } = ocrConfidence;
    
    public DocumentLineWordCoordinates Coordinates { get; set; } = coordinates;
    
    public string? HandwrittenOrTyped { get; set; } = handwrittenOrTyped;
    
    public bool Autocorrected { get; set; }
}