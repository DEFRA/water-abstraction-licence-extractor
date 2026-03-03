namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class ValueWithConfidence<T>
{
    public ValueWithConfidence() {}

    public ValueWithConfidence(T? value, double? ocrConfidence, double confidence)
    {
        Value = value;
        OcrConfidence = ocrConfidence;
        Confidence = confidence;
    }
    
    public T? Value { get; set; }
    
    public double? OcrConfidence { get; set; }
    
    public double? Confidence { get; set; }
}