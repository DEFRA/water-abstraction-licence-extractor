namespace WALE.ProcessFile.Core.Enums;

/// <summary>
/// What the extractor should do when an OCR provider throws for an image.
/// </summary>
public enum OcrServiceFailurePolicy
{
    DegradeOnError,
    FailFileOnTransientError
}
