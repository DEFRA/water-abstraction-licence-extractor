namespace WALE.ProcessFile.Core.Enums;

public enum ConfidenceType
{
    NotSet,
    OcrConfidencePassthrough,
    OcrConfidenceMultiplied,
    OcrConfidenceMultipliedMinusNPerLine,
    OcrConfidencePassthroughMinusNPerLine,
    Static
}