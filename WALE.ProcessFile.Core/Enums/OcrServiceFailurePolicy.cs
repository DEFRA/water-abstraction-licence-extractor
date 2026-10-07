namespace WALE.ProcessFile.Core.Enums;

/// <summary>
/// What the extractor should do when an OCR provider throws for an image.
/// </summary>
public enum OcrServiceFailurePolicy
{
    /// <summary>
    /// Record the failure on the result and carry on with the other providers and pages.
    /// </summary>
    DegradeOnError,

    /// <summary>
    /// Record the failure and fail the whole file when the provider reports a transient
    /// error (throttling, 5xx). 
    /// </summary>
    FailFileOnTransientError
}
