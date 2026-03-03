using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models;

public class DocumentLineWord
{
    public DocumentLineWord(
        string text,
        double? ocrConfidence,
        DocumentLineWordCoordinates coordinates,
        string? handwrittenOrTyped)
    {
        Text = text;
        OcrConfidence = ocrConfidence;
        Coordinates = coordinates;
        HandwrittenOrTyped = handwrittenOrTyped;
    }
    
    [JsonConstructor]
    public DocumentLineWord() :
        this(string.Empty, null, DocumentLineWordCoordinates.NotKnown(), null) { }

    private string? _text;
    
    public string Text
    {
        get => _text!;
        set
        {
            var trimmedText = value.Trim();
            
            if (trimmedText != " " && trimmedText.Contains(' '))
            {
                throw new Exception($"Word cannot contain space ('{trimmedText}')");
            }
            
            _text = trimmedText;
        }
    }

    public double? OcrConfidence { get; set; }
    
    public DocumentLineWordCoordinates Coordinates { get; set; }
    
    public string? HandwrittenOrTyped { get; set; }
    
    public bool Autocorrected { get; set; }
    
    public DocumentLineWord Clone()
    {
        var cloned = new DocumentLineWord(Text, OcrConfidence, Coordinates, HandwrittenOrTyped)
        {
            Autocorrected = Autocorrected
        };

        return cloned;
    }
}