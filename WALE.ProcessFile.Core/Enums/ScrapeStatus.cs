namespace WALE.ProcessFile.Core.Enums;

public enum ScrapeStatus
{
    Ok,
    NotFound,
    PathMissing,
    FileIdMissing,
    InProgress,
    Error,

    /// <summary>
    /// Processing failed for a reason expected to clear on its own (e.g. an OCR provider was
    /// throttled). Unlike <see cref="Error"/> this does not hold the per-run lock: a retry of the
    /// same file in the same process run reclaims the row and processes from scratch.
    /// </summary>
    TransientError
}