namespace WALE.ProcessFile.Core.Enums;

public enum ScrapeStatus
{
    Ok,
    NotFound,
    PathMissing,
    FileIdMissing,
    InProgress,
    Error,
    NotScraped,

    /// <summary>
    /// Unlike <see cref="Error"/> this does not hold the per-run lock: a retry of the same file
    /// in the same run reclaims the row and processes from scratch.
    /// </summary>
    TransientError
}
