namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class ValueWithConfidence<T>
{
    public ValueWithConfidence() {}

    public ValueWithConfidence(T? value)
    {
        Value = value;
    }
    
    public T? Value { get; set; }
    
    public double? Confidence { get; set; }
    
    public double? OcrConfidence { get; set; }
}