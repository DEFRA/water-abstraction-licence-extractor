namespace WALE.ProcessFile.Core.Enums.OutputSchema;

public enum ConfidenceType
{
    NotSet,
    OcrConfidencePassthrough,
    OcrConfidenceMultiplied,
    OcrConfidenceMultipliedMinusNPerLine,
    OcrConfidencePassthroughMinusNPerLine,
    Static
}